using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace HarmonyExLib
{
    /// <summary>A PatchClassProcessor used to turn <see cref="HarmonyExAttribute"/> on a class/type into patches</summary>
    /// 
    public class PatchClassProcessor
    {
        public readonly HarmonyEx instance;

        readonly Type containerType;
        readonly HarmonyExMethod containerAttributes;
        readonly Dictionary<Type, MethodInfo> auxilaryMethods;

        readonly List<AttributePatch> patchMethods;

        static readonly List<Type> auxilaryTypes = new()
        {
            typeof(HarmonyExPrepare),
            typeof(HarmonyExCleanup),
            typeof(HarmonyExTargetMethod),
            typeof(HarmonyExTargetMethods)
        };

        /// <summary name="Category">Name of the patch class's category</summary>
        public string Category { get; set; }

        /// <summary>Creates a patch class processor by pointing out a class. Similar to PatchAll() but without searching through all classes.</summary>
        /// <param name="instance">The HarmonyEx instance</param>
        /// <param name="type">The class to process (need to have at least a [HarmonyExPatch] attribute)</param>
        ///
        public PatchClassProcessor(HarmonyEx instance, Type type)
        {
            if (instance is null)
                throw new ArgumentNullException(nameof(instance));
            if (type is null)
                throw new ArgumentNullException(nameof(type));

            this.instance = instance;
            containerType = type;

            var harmonyAttributes = HarmonyExMethodExtensions.GetFromType(type);
            if (harmonyAttributes is null || harmonyAttributes.Count == 0)
                return;

            containerAttributes = HarmonyExMethod.Merge(harmonyAttributes);
            if (containerAttributes.methodType is null) // MethodType default is Normal
                containerAttributes.methodType = MethodType.Normal;

            this.Category = containerAttributes.category;

            auxilaryMethods = new Dictionary<Type, MethodInfo>();
            foreach (var auxType in auxilaryTypes)
            {
                var method = PatchTools.GetPatchMethod(containerType, auxType.FullName);
                if (method is object) auxilaryMethods[auxType] = method;
            }

            patchMethods = PatchTools.GetPatchMethods(containerType);
            foreach (var patchMethod in patchMethods)
            {
                var method = patchMethod.info.method;
                patchMethod.info = containerAttributes.Merge(patchMethod.info);
                patchMethod.info.method = method;
            }
        }
        /// <summary>Applies the patches</summary>
        /// <returns>A list of all created replacement methods or null if patch class is not annotated</returns>
        ///
        public List<MethodInfo> Patch(out List<HarmonyExMethod> cannotPatch)
        {
            cannotPatch = new List<HarmonyExMethod>();
            if (containerAttributes is null)
                return null;

            Exception exception = null;

            var mainPrepareResult = RunMethod<HarmonyExPrepare, bool>(true, false);
            if (mainPrepareResult is false)
            {
                RunMethod<HarmonyExCleanup>(ref exception);
                ReportException(exception, null);
                return new List<MethodInfo>();
            }

            var replacements = new List<MethodInfo>();
            MethodBase lastOriginal = null;
            try
            {
                var originals = GetBulkMethods();

                if (originals.Count == 1)
                    lastOriginal = originals[0];
                ReversePatch(ref lastOriginal);

                replacements = originals.Count > 0 ? BulkPatch(originals, ref lastOriginal) : PatchWithAttributes(ref lastOriginal, out cannotPatch);
            }
            catch (Exception ex)
            {
                exception = ex;
            }

            RunMethod<HarmonyExCleanup>(ref exception, exception);
            ReportException(exception, lastOriginal);
            return replacements;
        }
        public void Unpatch()
        {
            LoadJobs();
            foreach (var job in jobs.GetJobs())
                ProcessUnpatchJob(job);
        }
        void ReversePatch(ref MethodBase lastOriginal)
        {
            for (var i = 0; i < patchMethods.Count; i++)
            {
                var patchMethod = patchMethods[i];
                if (patchMethod.type == HarmonyExPatchType.ReversePatch)
                {
                    var annotatedOriginal = patchMethod.info.GetOriginalMethod();
                    if (annotatedOriginal is object)
                        lastOriginal = annotatedOriginal;
                    var reversePatcher = instance.CreateReversePatcher(lastOriginal, patchMethod.info);
                    lock (PatchProcessor.locker)
                        _ = reversePatcher.Patch();
                }
            }
        }
        PatchJobs<MethodInfo> jobs;
        void LoadJobs(List<MethodBase> originals = null)
        {
            if (jobs != null) return;
            jobs = new PatchJobs<MethodInfo>();
            if (originals == null)
            {
                foreach (var patchMethod in patchMethods)
                {
                    var method = patchMethod.info.GetOriginalMethod();
                    if (method == null) continue;
                    var job = jobs.GetJob(method);
                    job.AddPatch(patchMethod);
                }
            }
            else
            {
                foreach (var original in originals)
                {
                    var job = jobs.GetJob(original);
                    foreach (var patchMethod in patchMethods)
                    {
                        var info = patchMethod.info;
                        if (info.methodName is object) continue;
                        if (info.methodType.HasValue && info.methodType.Value != MethodType.Normal) continue;
                        if (info.argumentTypes is object) continue;
                        job.AddPatch(patchMethod);
                    }
                }
            }
        }
        List<MethodInfo> BulkPatch(List<MethodBase> originals, ref MethodBase lastOriginal)
        {
            jobs = new PatchJobs<MethodInfo>();
            for (var i = 0; i < originals.Count; i++)
            {
                lastOriginal = originals[i];
                var job = jobs.GetJob(lastOriginal);
                foreach (var patchMethod in patchMethods)
                {
                    const string note = "You cannot combine TargetMethod, TargetMethods or [HarmonyExPatchAll] with individual annotations";
                    var info = patchMethod.info;
                    if (info.methodName is object)
                        throw new ArgumentException($"{note} [{info.methodName}]");
                    if (info.methodType.HasValue && info.methodType.Value != MethodType.Normal)
                        throw new ArgumentException($"{note} [{info.methodType}]");
                    if (info.argumentTypes is object)
                        throw new ArgumentException($"{note} [{info.argumentTypes.Description()}]");

                    job.AddPatch(patchMethod);
                }
            }
            foreach (var job in jobs.GetJobs())
            {
                lastOriginal = job.original;
                ProcessPatchJob(job);
            }
            return jobs.GetReplacements();
        }

        List<MethodInfo> PatchWithAttributes(ref MethodBase lastOriginal, out List<HarmonyExMethod> notFound)
        {
            notFound = new List<HarmonyExMethod>();
            jobs = new PatchJobs<MethodInfo>();
            foreach (var patchMethod in patchMethods)
            {
                lastOriginal = patchMethod.info.GetOriginalMethod();
                if (lastOriginal is null)
                {
                    notFound.Add(patchMethod.info);
                    continue;
                }
                //throw new ArgumentException($"Undefined target method for patch method {patchMethod.info.method.FullDescription()}");

                var job = jobs.GetJob(lastOriginal);
                job.AddPatch(patchMethod);
            }
            foreach (var job in jobs.GetJobs())
            {
                lastOriginal = job.original;
                ProcessPatchJob(job);
            }
            return jobs.GetReplacements();
        }

        void ProcessPatchJob(PatchJobs<MethodInfo>.Job job)
        {
            MethodInfo replacement = default;

            var individualPrepareResult = RunMethod<HarmonyExPrepare, bool>(true, false, null, job.original);
            Exception exception = null;
            if (individualPrepareResult)
            {
                lock (PatchProcessor.locker)
                {
                    try
                    {
                        var patchInfo = HarmonySharedState.GetPatchInfo(job.original) ?? new PatchInfo();
                        patchInfo.AddPrefixes(instance.Id, Tools.DistinctPatches(patchInfo.prefixes, job.prefixes).ToArray());
                        patchInfo.AddPostfixes(instance.Id, Tools.DistinctPatches(patchInfo.postfixes, job.postfixes).ToArray());
                        patchInfo.AddTranspilers(instance.Id, Tools.DistinctPatches(patchInfo.transpilers, job.transpilers).ToArray());
                        patchInfo.AddFinalizers(instance.Id, Tools.DistinctPatches(patchInfo.finalizers, job.finalizers).ToArray());

                        replacement = PatchFunctions.UpdateWrapper(job.original, patchInfo);
                        HarmonySharedState.UpdatePatchInfo(job.original, replacement, patchInfo);
                    }
                    catch (Exception ex)
                    {
                        exception = ex;
                    }
                }
            }
            RunMethod<HarmonyExCleanup>(ref exception, job.original, exception);
            ReportException(exception, job.original);
            job.replacement = replacement;
        }
        void ProcessUnpatchJob(PatchJobs<MethodInfo>.Job job)
        {
            MethodInfo replacement = default;
            Exception exception = null;
            lock (PatchProcessor.locker)
            {
                try
                {
                    var patchInfo = HarmonySharedState.GetPatchInfo(job.original) ?? new PatchInfo();
                    patchInfo.RemovePatches(job.prefixes.Concat(job.postfixes).Concat(job.transpilers).Concat(job.finalizers).Select(m => m.method).ToArray());
                    replacement = PatchFunctions.UpdateWrapper(job.original, patchInfo);
                    HarmonySharedState.UpdatePatchInfo(job.original, replacement, patchInfo);
                }
                catch (Exception ex)
                {
                    exception = ex;
                }
            }
            ReportException(exception, job.original);
            job.replacement = replacement;
        }
        public List<MethodBase> GetBulkMethods()
        {
            var isPatchAll = containerType.GetCustomAttributes(true).Any(a => a.GetType().FullName == typeof(HarmonyExPatchAll).FullName);
            if (isPatchAll)
            {
                var type = containerAttributes.declaringType;
                if (type is null)
                    throw new ArgumentException($"Using {typeof(HarmonyExPatchAll).FullName} requires an additional attribute for specifying the Class/Type");

                var list = new List<MethodBase>();
                list.AddRange(AccessTools.GetDeclaredConstructors(type).Cast<MethodBase>());
                list.AddRange(AccessTools.GetDeclaredMethods(type).Cast<MethodBase>());
                var props = AccessTools.GetDeclaredProperties(type);
                list.AddRange(props.Select(prop => prop.GetGetMethod(true)).Where(method => method is object).Cast<MethodBase>());
                list.AddRange(props.Select(prop => prop.GetSetMethod(true)).Where(method => method is object).Cast<MethodBase>());
                return list;
            }

            static string FailOnResult(IEnumerable<MethodBase> res)
            {
                if (res is null) return "null";
                if (res.Any(m => m is null)) return "some element was null";
                return null;
            }
            var targetMethods = RunMethod<HarmonyExTargetMethods, IEnumerable<MethodBase>>(null, null, FailOnResult);
            if (targetMethods is object)
                return targetMethods.ToList();

            var result = new List<MethodBase>();
            var targetMethod = RunMethod<HarmonyExTargetMethod, MethodBase>(null, null, method => method is null ? "null" : null);
            if (targetMethod is object)
                result.Add(targetMethod);
            return result;
        }

        void ReportException(Exception exception, MethodBase original)
        {
            if (exception is null) return;
            if ((containerAttributes.debug ?? false) || HarmonyEx.DEBUG)
            {
                _ = HarmonyEx.VersionInfo(out var currentVersion);

                FileLog.indentLevel = 0;
                FileLog.Log($"### Exception from user \"{instance.Id}\", HarmonyEx v{currentVersion}");
                FileLog.Log($"### Original: {(original?.FullDescription() ?? "NULL")}");
                FileLog.Log($"### Patch class: {containerType.FullDescription()}");
                var logException = exception;
                if (logException is HarmonyExLibception hEx) logException = hEx.InnerException;
                var exStr = logException.ToString();
                while (exStr.Contains("\n\n"))
                    exStr = exStr.Replace("\n\n", "\n");
                exStr = exStr.Split('\n').Join(line => $"### {line}", "\n");
                FileLog.Log(exStr.Trim());
            }

            if (exception is HarmonyExLibception) throw exception; // assume HarmonyExLibception already wraps the actual exception
            throw new HarmonyExLibception($"Patching exception in method {original.FullDescription()}", exception);
        }

        T RunMethod<S, T>(T defaultIfNotExisting, T defaultIfFailing, Func<T, string> failOnResult = null, params object[] parameters)
        {
            if (auxilaryMethods.TryGetValue(typeof(S), out var method))
            {
                var input = (parameters ?? new object[0]).Union(new object[] { instance }).ToArray();
                var actualParameters = AccessTools.ActualParameters(method, input);

                if (method.ReturnType != typeof(void) && typeof(T).IsAssignableFrom(method.ReturnType) is false)
                    throw new Exception($"Method {method.FullDescription()} has wrong return type (should be assignable to {typeof(T).FullName})");

                var result = defaultIfFailing;
                try
                {
                    if (method.ReturnType == typeof(void))
                    {
                        _ = method.Invoke(null, actualParameters);
                        result = defaultIfNotExisting;
                    }
                    else
                        result = (T)method.Invoke(null, actualParameters);

                    if (failOnResult is object)
                    {
                        var error = failOnResult(result);
                        if (error is object)
                            throw new Exception($"Method {method.FullDescription()} returned an unexpected result: {error}");
                    }
                }
                catch (Exception ex)
                {
                    ReportException(ex, method);
                }
                return result;
            }

            return defaultIfNotExisting;
        }

        void RunMethod<S>(ref Exception exception, params object[] parameters)
        {
            if (auxilaryMethods.TryGetValue(typeof(S), out var method))
            {
                var input = (parameters ?? new object[0]).Union(new object[] { instance }).ToArray();
                var actualParameters = AccessTools.ActualParameters(method, input);
                try
                {
                    var result = method.Invoke(null, actualParameters);
                    if (method.ReturnType == typeof(Exception))
                        exception = result as Exception;
                }
                catch (Exception ex)
                {
                    ReportException(ex, method);
                }
            }
        }
    }
}
