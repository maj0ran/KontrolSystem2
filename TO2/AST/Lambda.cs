﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using KontrolSystem.Parsing;
using KontrolSystem.TO2.Generator;
using KontrolSystem.TO2.Runtime;

namespace KontrolSystem.TO2.AST;

internal readonly struct LambdaClass {
    internal readonly List<(string sourceName, ClonedFieldVariable target)> clonedVariables;
    internal readonly ConstructorInfo constructor;
    internal readonly MethodInfo lambdaImpl;

    internal LambdaClass(List<(string sourceName, ClonedFieldVariable target)> clonedVariables,
        ConstructorInfo constructor, MethodInfo lambdaImpl) {
        this.clonedVariables = clonedVariables;
        this.constructor = constructor;
        this.lambdaImpl = lambdaImpl;
    }
}

public class Lambda : Expression, IVariableContainer {
    private readonly Expression expression;
    private readonly List<FunctionParameter> parameters;
    private LambdaClass? lambdaClass;

    private FunctionType? resolvedType;

    private TypeHint? typeHint;

    public Lambda(List<FunctionParameter> parameters, Expression expression, Position start = new(),
        Position end = new()) : base(start, end) {
        this.parameters = parameters;
        if (expression is Block b)
            this.expression = b.CollapseFinalReturn();
        else
            this.expression = expression;
        this.expression.VariableContainer = this;
    }

    public override IVariableContainer? VariableContainer {
        set {
            ParentContainer = value;
            expression.VariableContainer = this;
        }
    }

    public override TypeHint? TypeHint {
        set => typeHint = value;
    }

    public IVariableContainer? ParentContainer { get; private set; }

    public TO2Type? FindVariableLocal(IBlockContext context, string name) {
        var idx = parameters.FindIndex(p => p.name == name);

        if (idx < 0 || idx >= parameters.Count) return null;

        var parameterType = parameters[idx].type;
        if (parameterType != null) return parameterType;
        if (resolvedType == null || idx >= resolvedType.parameterTypes.Count) return null;

        return resolvedType.parameterTypes[idx];
    }

    public override void Prepare(IBlockContext context) {
    }

    public override TO2Type ResultType(IBlockContext context) {
        if (resolvedType != null) return resolvedType;
        // Make an assumption ...
        if (parameters.All(p => p.type != null))
            resolvedType = new FunctionType(false, parameters.Select(p => p.type!).ToList(), BuiltinType.Unit);
        else resolvedType = typeHint?.Invoke(context) as FunctionType;
        if (resolvedType != null) {
            // ... so that it is possible to determine the return type
            var returnType = expression.ResultType(context);
            // ... so that the assumption can be replaced with the (hopefully) real thing
            resolvedType = new FunctionType(false, resolvedType.parameterTypes, returnType);
        }

        return resolvedType ?? BuiltinType.Unit;
    }

    public override void EmitCode(IBlockContext context, bool dropResult) {
        var lambdaType = ResultType(context) as FunctionType;

        if (lambdaType == null) {
            context.AddError(new StructuralError(
                StructuralError.ErrorType.InvalidType,
                "Unable to infer type of lambda. Please add some type hint",
                Start,
                End
            ));
            return;
        }

        if (lambdaType.parameterTypes.Count != parameters.Count)
            context.AddError(new StructuralError(
                StructuralError.ErrorType.InvalidType,
                $"Expected lambda to have {lambdaType.parameterTypes.Count} parameters, found {parameters.Count}",
                Start,
                End
            ));

        for (var i = 0; i < parameters.Count; i++) {
            if (parameters[i].type == null) continue;
            if (!lambdaType.parameterTypes[i].UnderlyingType(context.ModuleContext)
                    .IsAssignableFrom(context.ModuleContext, parameters[i].type!.UnderlyingType(context.ModuleContext)))
                context.AddError(new StructuralError(
                    StructuralError.ErrorType.InvalidType,
                    $"Expected parameter {parameters[i].name} of lambda to have type {lambdaType.parameterTypes[i]}, found {parameters[i].type}",
                    Start,
                    End
                ));
        }

        if (context.HasErrors) return;

        if (dropResult) return;

        lambdaClass ??= CreateLambdaClass(context, lambdaType);

        foreach (var (sourceName, _) in lambdaClass.Value.clonedVariables) {
            var source = context.FindVariable(sourceName)!;
            source.EmitLoad(context);
        }

        context.IL.EmitNew(OpCodes.Newobj, lambdaClass.Value.constructor, lambdaClass.Value.clonedVariables.Count);
        context.IL.EmitPtr(OpCodes.Ldftn, lambdaClass.Value.lambdaImpl);
        context.IL.EmitNew(OpCodes.Newobj,
            lambdaType.GeneratedType(context.ModuleContext)
                .GetConstructor(new[] { typeof(object), typeof(IntPtr) })!);
    }

    private LambdaClass CreateLambdaClass(IBlockContext parent, FunctionType lambdaType) {
        var lambdaModuleContext =
            parent.ModuleContext.DefineSubContext($"Lambda{Start.position}", typeof(object));

        var lambdaContext = new SyncBlockContext(lambdaModuleContext, false, "LambdaImpl",
            lambdaType.returnType, FixedParameters(lambdaType));
        var clonedVariables =
            new SortedDictionary<string, (string sourceName, ClonedFieldVariable target)>();

        lambdaContext.ExternVariables = name => {
            if (clonedVariables.ContainsKey(name)) return clonedVariables[name].target;
            var externalVariable = parent.FindVariable(name);
            if (externalVariable == null) return null;
            if (!externalVariable.IsConst) {
                lambdaContext.AddError(new StructuralError(StructuralError.ErrorType.NoSuchVariable,
                    $"Outer variable {name} is not const. Only read-only variables can be referenced in a lambda expression",
                    Start, End));
                return null;
            }

            var field = lambdaModuleContext.typeBuilder.DefineField(name,
                externalVariable.Type.GeneratedType(parent.ModuleContext),
                FieldAttributes.InitOnly | FieldAttributes.Private);
            var clonedVariable = new ClonedFieldVariable(externalVariable.Type, field);
            clonedVariables.Add(name, (externalVariable.Name, clonedVariable));
            return clonedVariable;
        };

        expression.EmitCode(lambdaContext, false);
        lambdaContext.IL.EmitReturn(lambdaContext.MethodBuilder!.ReturnType);

        foreach (var error in lambdaContext.AllErrors) parent.AddError(error);

        var lambdaFields = clonedVariables.Values.Select(c => c.target.valueField).ToList();
        var constructorBuilder = lambdaModuleContext.typeBuilder.DefineConstructor(
            MethodAttributes.Public, CallingConventions.Standard,
            lambdaFields.Select(f => f.FieldType).ToArray());
        IILEmitter constructorEmitter = new GeneratorILEmitter(constructorBuilder.GetILGenerator());

        var argIndex = 1;
        foreach (var field in lambdaFields) {
            constructorEmitter.Emit(OpCodes.Ldarg_0);
            MethodParameter.EmitLoadArg(constructorEmitter, argIndex++);
            constructorEmitter.Emit(OpCodes.Stfld, field);
        }

        constructorEmitter.EmitReturn(typeof(void));

        lambdaType.GeneratedType(parent.ModuleContext);

        return new LambdaClass(clonedVariables.Values.ToList(), constructorBuilder, lambdaContext.MethodBuilder);
    }

    private List<FunctionParameter> FixedParameters(FunctionType lambdaType) {
        return parameters.Zip(lambdaType.parameterTypes, (p, f) => new FunctionParameter(p.name, p.type ?? f, null))
            .ToList();
    }

    public override REPLValueFuture Eval(REPLContext context) {
        throw new NotSupportedException("Lambda are not supported in REPL mode");
    }
}
