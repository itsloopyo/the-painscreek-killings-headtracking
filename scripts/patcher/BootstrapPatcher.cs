using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Mono.Cecil;
using Mono.Cecil.Cil;

public static class BootstrapPatcher
{
    private const string PatchMarker = "HeadTracking_Patched_Painscreek_v2";
    private const string BootstrapTypeName = "HeadTrackingBootstrap";

    public static bool PatchAssembly(string assemblyPath)
    {
        string managedDir = Path.GetDirectoryName(assemblyPath);

        var resolver = new DefaultAssemblyResolver();
        resolver.AddSearchDirectory(managedDir);
        var parentDir = Path.GetDirectoryName(managedDir);
        if (!string.IsNullOrEmpty(parentDir))
        {
            resolver.AddSearchDirectory(parentDir);
        }

        var readerParams = new ReaderParameters
        {
            AssemblyResolver = resolver,
            ReadWrite = false,
            InMemory = true
        };

        byte[] assemblyBytes = File.ReadAllBytes(assemblyPath);
        using (var memStream = new MemoryStream(assemblyBytes))
        using (var assembly = AssemblyDefinition.ReadAssembly(memStream, readerParams))
        {
            if (assembly.MainModule.Types.Any(t => t.Name == PatchMarker))
            {
                Console.WriteLine("  Already patched - skipping");
                return true;
            }

            var bootstrapType = new TypeDefinition(
                "PainscreekHeadTracking",
                BootstrapTypeName,
                TypeAttributes.NotPublic | TypeAttributes.Class | TypeAttributes.Sealed | TypeAttributes.Abstract,
                assembly.MainModule.TypeSystem.Object);

            var initializedField = new FieldDefinition(
                "_initialized",
                FieldAttributes.Private | FieldAttributes.Static,
                assembly.MainModule.TypeSystem.Boolean);
            bootstrapType.Fields.Add(initializedField);

            var mscorlibRef = assembly.MainModule.AssemblyReferences.FirstOrDefault(r => r.Name == "mscorlib");
            var mscorlib = resolver.Resolve(mscorlibRef);

            var assemblyType = mscorlib.MainModule.Types.First(t => t.FullName == "System.Reflection.Assembly");
            var loadFromMethod = assemblyType.Methods.First(m => m.Name == "LoadFrom" && m.Parameters.Count == 1);
            var getTypeMethod = assemblyType.Methods.First(m => m.Name == "GetType" && m.Parameters.Count == 1);
            var loadFromRef = assembly.MainModule.ImportReference(loadFromMethod);
            var getTypeRef = assembly.MainModule.ImportReference(getTypeMethod);

            var typeType = mscorlib.MainModule.Types.First(t => t.FullName == "System.Type");
            var getMethodMethod = typeType.Methods.First(m => m.Name == "GetMethod" && m.Parameters.Count == 1);
            var getMethodRef = assembly.MainModule.ImportReference(getMethodMethod);

            var methodBaseType = mscorlib.MainModule.Types.First(t => t.FullName == "System.Reflection.MethodBase");
            var methodInfoType = mscorlib.MainModule.Types.First(t => t.FullName == "System.Reflection.MethodInfo");
            var invokeMethod = methodBaseType.Methods.First(m => m.Name == "Invoke" && m.Parameters.Count == 2);
            var invokeRef = assembly.MainModule.ImportReference(invokeMethod);
            var methodInfoTypeRef = assembly.MainModule.ImportReference(methodInfoType);

            var pathType = mscorlib.MainModule.Types.First(t => t.FullName == "System.IO.Path");
            var getDirectoryNameMethod = pathType.Methods.First(m => m.Name == "GetDirectoryName");
            var combineMethod = pathType.Methods.First(m => m.Name == "Combine" && m.Parameters.Count == 2);
            var getDirectoryNameRef = assembly.MainModule.ImportReference(getDirectoryNameMethod);
            var combineRef = assembly.MainModule.ImportReference(combineMethod);

            var exceptionType = mscorlib.MainModule.Types.First(t => t.FullName == "System.Exception");
            var toStringMethod = exceptionType.Methods.First(m => m.Name == "ToString" && m.Parameters.Count == 0);
            var toStringRef = assembly.MainModule.ImportReference(toStringMethod);

            var fileType = mscorlib.MainModule.Types.First(t => t.FullName == "System.IO.File");
            var appendAllTextMethod = fileType.Methods.First(m => m.Name == "AppendAllText" && m.Parameters.Count == 2);
            var appendAllTextRef = assembly.MainModule.ImportReference(appendAllTextMethod);

            var stringType = mscorlib.MainModule.Types.First(t => t.FullName == "System.String");
            var concatMethod = stringType.Methods.First(m => m.Name == "Concat" && m.Parameters.Count == 2
                && m.Parameters[0].ParameterType.FullName == "System.String");
            var concatRef = assembly.MainModule.ImportReference(concatMethod);

            var getLocationMethod = assemblyType.Properties.First(p => p.Name == "Location").GetMethod;
            var getLocationRef = assembly.MainModule.ImportReference(getLocationMethod);

            var getExecutingAssemblyMethod = assemblyType.Methods.First(m => m.Name == "GetExecutingAssembly");
            var getExecutingAssemblyRef = assembly.MainModule.ImportReference(getExecutingAssemblyMethod);

            var tempPathMethod = pathType.Methods.First(m => m.Name == "GetTempPath");
            var getTempPathRef = assembly.MainModule.ImportReference(tempPathMethod);

            // Static field holding the cached MethodInfo for PainscreekHeadTracking.StaticTracker.ApplyTracking.
            // Set during Initialize, invoked every frame from FirstPersonPlayerController.LateUpdate via the
            // ApplyTracking wrapper method emitted below. Caching here avoids per-frame reflection lookups
            // and avoids per-frame Assembly.LoadFrom calls.
            var applyMethodField = new FieldDefinition(
                "_applyMethod",
                FieldAttributes.Private | FieldAttributes.Static,
                methodInfoTypeRef);
            bootstrapType.Fields.Add(applyMethodField);

            // ============================================================
            // Initialize method - called from FPC.Start. Loads the mod
            // assembly, kicks off ModLoader.Initialize, and caches the
            // StaticTracker.ApplyTracking MethodInfo for per-frame use.
            // ============================================================

            var initMethod = new MethodDefinition(
                "Initialize",
                MethodAttributes.Public | MethodAttributes.Static | MethodAttributes.HideBySig,
                assembly.MainModule.TypeSystem.Void);

            var il = initMethod.Body.GetILProcessor();
            initMethod.Body.InitLocals = true;

            initMethod.Body.Variables.Add(new VariableDefinition(assembly.MainModule.TypeSystem.String));
            initMethod.Body.Variables.Add(new VariableDefinition(assembly.MainModule.TypeSystem.String));
            initMethod.Body.Variables.Add(new VariableDefinition(assembly.MainModule.TypeSystem.Object));
            initMethod.Body.Variables.Add(new VariableDefinition(assembly.MainModule.TypeSystem.Object));
            initMethod.Body.Variables.Add(new VariableDefinition(assembly.MainModule.TypeSystem.Object));
            initMethod.Body.Variables.Add(new VariableDefinition(assembly.MainModule.TypeSystem.Object));

            var retInstruction = il.Create(OpCodes.Ret);
            var tryStart = il.Create(OpCodes.Nop);
            var catchStart = il.Create(OpCodes.Nop);

            il.Append(il.Create(OpCodes.Ldsfld, initializedField));
            il.Append(il.Create(OpCodes.Brtrue, retInstruction));

            il.Append(il.Create(OpCodes.Ldc_I4_1));
            il.Append(il.Create(OpCodes.Stsfld, initializedField));

            il.Append(tryStart);

            il.Append(il.Create(OpCodes.Call, getExecutingAssemblyRef));
            il.Append(il.Create(OpCodes.Callvirt, getLocationRef));
            il.Append(il.Create(OpCodes.Call, getDirectoryNameRef));
            il.Append(il.Create(OpCodes.Stloc_0));

            il.Append(il.Create(OpCodes.Ldloc_0));
            il.Append(il.Create(OpCodes.Ldstr, "PainscreekHeadTracking.dll"));
            il.Append(il.Create(OpCodes.Call, combineRef));
            il.Append(il.Create(OpCodes.Stloc_1));

            il.Append(il.Create(OpCodes.Ldloc_0));
            il.Append(il.Create(OpCodes.Ldstr, "\\HeadTracking_BOOT.log"));
            il.Append(il.Create(OpCodes.Call, concatRef));
            il.Append(il.Create(OpCodes.Ldstr, "Loading PainscreekHeadTracking.dll...\n"));
            il.Append(il.Create(OpCodes.Call, appendAllTextRef));

            il.Append(il.Create(OpCodes.Ldloc_1));
            il.Append(il.Create(OpCodes.Call, loadFromRef));
            il.Append(il.Create(OpCodes.Stloc_2));

            il.Append(il.Create(OpCodes.Ldloc_2));
            il.Append(il.Create(OpCodes.Ldstr, "PainscreekHeadTracking.ModLoader"));
            il.Append(il.Create(OpCodes.Callvirt, getTypeRef));
            il.Append(il.Create(OpCodes.Stloc_3));

            il.Append(il.Create(OpCodes.Ldloc_3));
            il.Append(il.Create(OpCodes.Ldstr, "Initialize"));
            il.Append(il.Create(OpCodes.Callvirt, getMethodRef));
            il.Append(il.Create(OpCodes.Stloc, 4));

            il.Append(il.Create(OpCodes.Ldloc, 4));
            il.Append(il.Create(OpCodes.Ldnull));
            il.Append(il.Create(OpCodes.Ldnull));
            il.Append(il.Create(OpCodes.Callvirt, invokeRef));
            il.Append(il.Create(OpCodes.Pop));

            // Cache StaticTracker.ApplyTracking MethodInfo into _applyMethod.
            // Reuse loc_2 (loaded assembly), loc_3 (Type), loc_4 (MethodInfo).
            il.Append(il.Create(OpCodes.Ldloc_2));
            il.Append(il.Create(OpCodes.Ldstr, "PainscreekHeadTracking.StaticTracker"));
            il.Append(il.Create(OpCodes.Callvirt, getTypeRef));
            il.Append(il.Create(OpCodes.Stloc_3));

            il.Append(il.Create(OpCodes.Ldloc_3));
            il.Append(il.Create(OpCodes.Ldstr, "ApplyTracking"));
            il.Append(il.Create(OpCodes.Callvirt, getMethodRef));
            il.Append(il.Create(OpCodes.Stsfld, applyMethodField));

            il.Append(il.Create(OpCodes.Ldloc_0));
            il.Append(il.Create(OpCodes.Ldstr, "\\HeadTracking_BOOT.log"));
            il.Append(il.Create(OpCodes.Call, concatRef));
            il.Append(il.Create(OpCodes.Ldstr, "SUCCESS: ModLoader.Initialize() called and ApplyTracking cached\n"));
            il.Append(il.Create(OpCodes.Call, appendAllTextRef));

            var leaveTarget = il.Create(OpCodes.Ret);
            il.Append(il.Create(OpCodes.Leave, leaveTarget));

            il.Append(catchStart);
            il.Append(il.Create(OpCodes.Stloc, 5));

            il.Append(il.Create(OpCodes.Call, getTempPathRef));
            il.Append(il.Create(OpCodes.Ldstr, "HeadTracking_BOOT_ERROR.log"));
            il.Append(il.Create(OpCodes.Call, combineRef));

            il.Append(il.Create(OpCodes.Ldstr, "ERROR: "));
            il.Append(il.Create(OpCodes.Ldloc, 5));
            il.Append(il.Create(OpCodes.Callvirt, toStringRef));
            il.Append(il.Create(OpCodes.Call, concatRef));
            il.Append(il.Create(OpCodes.Ldstr, "\n"));
            il.Append(il.Create(OpCodes.Call, concatRef));
            il.Append(il.Create(OpCodes.Call, appendAllTextRef));

            il.Append(il.Create(OpCodes.Leave, leaveTarget));

            il.Append(leaveTarget);

            var initHandler = new ExceptionHandler(ExceptionHandlerType.Catch)
            {
                TryStart = tryStart,
                TryEnd = catchStart,
                HandlerStart = catchStart,
                HandlerEnd = leaveTarget,
                CatchType = assembly.MainModule.ImportReference(exceptionType)
            };
            initMethod.Body.ExceptionHandlers.Add(initHandler);

            bootstrapType.Methods.Add(initMethod);

            // ============================================================
            // ApplyTracking wrapper - injected into FPC.LateUpdate. Reads
            // the cached _applyMethod field and invokes it. Catches and
            // swallows exceptions so the game's LateUpdate keeps running
            // even if our code throws (StaticTracker.ApplyTracking has its
            // own throttled error logging; this catch is just a hard barrier).
            // ============================================================

            var applyMethod = new MethodDefinition(
                "ApplyTracking",
                MethodAttributes.Public | MethodAttributes.Static | MethodAttributes.HideBySig,
                assembly.MainModule.TypeSystem.Void);
            applyMethod.Body.InitLocals = true;
            applyMethod.Body.Variables.Add(new VariableDefinition(assembly.MainModule.TypeSystem.Object));

            var applyIl = applyMethod.Body.GetILProcessor();
            var applyRet = applyIl.Create(OpCodes.Ret);
            var applyTryStart = applyIl.Create(OpCodes.Ldsfld, applyMethodField);
            var applyCatchStart = applyIl.Create(OpCodes.Stloc_0);

            applyIl.Append(applyTryStart);
            applyIl.Append(applyIl.Create(OpCodes.Brfalse, applyRet));

            applyIl.Append(applyIl.Create(OpCodes.Ldsfld, applyMethodField));
            applyIl.Append(applyIl.Create(OpCodes.Ldnull));
            applyIl.Append(applyIl.Create(OpCodes.Ldnull));
            applyIl.Append(applyIl.Create(OpCodes.Callvirt, invokeRef));
            applyIl.Append(applyIl.Create(OpCodes.Pop));

            applyIl.Append(applyIl.Create(OpCodes.Leave, applyRet));

            applyIl.Append(applyCatchStart);
            applyIl.Append(applyIl.Create(OpCodes.Leave, applyRet));

            applyIl.Append(applyRet);

            var applyHandler = new ExceptionHandler(ExceptionHandlerType.Catch)
            {
                TryStart = applyTryStart,
                TryEnd = applyCatchStart,
                HandlerStart = applyCatchStart,
                HandlerEnd = applyRet,
                CatchType = assembly.MainModule.ImportReference(exceptionType)
            };
            applyMethod.Body.ExceptionHandlers.Add(applyHandler);

            bootstrapType.Methods.Add(applyMethod);

            assembly.MainModule.Types.Add(bootstrapType);

            // ============================================================
            // Find FirstPersonPlayerController and inject the calls.
            // ============================================================

            string[] targetTypes = { "FirstPersonPlayerController", "PlayerController", "CameraControls", "PlayerControl", "GameManager", "MainManager", "GameController" };
            TypeDefinition targetType = null;
            string targetTypeName = null;

            foreach (var typeName in targetTypes)
            {
                targetType = assembly.MainModule.Types.FirstOrDefault(t =>
                    t.Name.Equals(typeName, StringComparison.OrdinalIgnoreCase) ||
                    t.Name.Contains(typeName));
                if (targetType != null)
                {
                    targetTypeName = typeName;
                    break;
                }
            }

            if (targetType == null)
            {
                Console.Error.WriteLine("  ERROR: Could not find target type to patch");
                return false;
            }

            Console.WriteLine("  Found: " + targetTypeName);

            var startMethod = targetType.Methods.FirstOrDefault(m => m.Name == "Start" && !m.IsStatic && m.HasBody);
            if (startMethod == null)
                startMethod = targetType.Methods.FirstOrDefault(m => m.Name == "Awake" && !m.IsStatic && m.HasBody);

            if (startMethod == null)
            {
                Console.Error.WriteLine("  ERROR: Could not find Start or Awake method");
                return false;
            }

            var startIL = startMethod.Body.GetILProcessor();
            var startFirst = startMethod.Body.Instructions.First();
            startIL.InsertBefore(startFirst, startIL.Create(OpCodes.Call, initMethod));
            Console.WriteLine("  Injected Initialize call into: " + targetTypeName + "." + startMethod.Name);

            // Inject ApplyTracking call before each Ret in LateUpdate. The wrinkle: existing
            // branches in the method body (e.g. brfalse from the early canMove/canRorate
            // checks, or the br at the end of the invert-Y branch) target the Ret instruction
            // directly. Naive InsertBefore leaves those branches still pointing at the Ret,
            // so they skip the injected call entirely - exactly what happened on the first
            // attempt: the ghost smear persisted because LateUpdate's branches bypassed our
            // call on the invert-Y path and the early-exit paths.
            //
            // Fix: capture branches targeting each Ret, insert the call before the Ret, then
            // redirect those branches to target the call. Any path that previously reached
            // the Ret now reaches the call first and falls through to Ret.
            var lateUpdate = targetType.Methods.FirstOrDefault(m => m.Name == "LateUpdate" && !m.IsStatic && m.HasBody);
            if (lateUpdate == null)
            {
                Console.Error.WriteLine("  WARNING: " + targetTypeName + ".LateUpdate not found - tracking will not be applied per-frame");
            }
            else
            {
                var lateIL = lateUpdate.Body.GetILProcessor();
                var allInstructions = lateUpdate.Body.Instructions.ToList();
                var retInstructions = allInstructions.Where(i => i.OpCode == OpCodes.Ret).ToList();

                foreach (var ret in retInstructions)
                {
                    var redirected = new List<Instruction>();
                    foreach (var instr in allInstructions)
                    {
                        if (instr == ret) continue;
                        if (ReferenceEquals(instr.Operand, ret)) redirected.Add(instr);
                    }

                    var callInstr = lateIL.Create(OpCodes.Call, applyMethod);
                    lateIL.InsertBefore(ret, callInstr);

                    foreach (var branch in redirected)
                    {
                        branch.Operand = callInstr;
                    }
                }
                Console.WriteLine("  Injected ApplyTracking call into: " + targetTypeName + ".LateUpdate (" + retInstructions.Count + " return site(s))");
            }

            var markerType = new TypeDefinition(
                "PainscreekHeadTracking",
                PatchMarker,
                TypeAttributes.NotPublic | TypeAttributes.Class,
                assembly.MainModule.TypeSystem.Object);
            assembly.MainModule.Types.Add(markerType);

            assembly.Write(assemblyPath);
            Console.WriteLine("  Patch complete!");
            return true;
        }
    }
}
