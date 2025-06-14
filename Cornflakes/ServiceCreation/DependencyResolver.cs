using System.Linq.Expressions;
using System.Reflection;
using System.Reflection.Emit;
using Cornflakes.Extensions;

namespace Cornflakes.ServiceCreation;

public static class DependencyResolver
{
    public static IServiceFactoryBuilder<TService> GetServiceFactory<TService, TImplementation>()
    {
        return GenerateFactory<TService>(typeof(TImplementation)).ToFactory();
    }

    internal static ServiceInitializer? TryGetMemberInjector<TImplementation>()
    {
        return TryCreateMemberInjector(typeof(TImplementation));
    }

    private static ServiceCreator<TService> GenerateFactory<TService>(Type implementationType)
    {
        ParameterExpression serviceProviderParameter = Expression.Parameter(typeof(IServiceProvider), "serviceProvider");
        MethodInfo? GetService = typeof(IServiceProvider)
            .GetMethod(nameof(IServiceProvider.GetService));

        if (GetService == null)
        {
            throw new MissingMethodException(nameof(IServiceProvider), nameof(IServiceProvider.GetService));
        }

        ConstructorInfo constructor = implementationType.GetConstructors().First();
        IEnumerable<Expression> arguments = constructor.GetParameters()
            .Select(p => Expression.Convert(
                Expression.Call(
                    serviceProviderParameter,
                    GetService,
                    Expression.Constant(p.ParameterType)),
                p.ParameterType));

        Expression constructionExpression = Expression.New(constructor, arguments);

        return Expression.Lambda<ServiceCreator<TService>>(constructionExpression, serviceProviderParameter).Compile();
    }

    private static ServiceCreationWrapper<TService> GenerateCreationWrapper<TService>(Type implementationType)
    {
        ParameterExpression serviceProviderParameter = Expression.Parameter(typeof(IServiceProvider), "serviceProvider");
        ParameterExpression instanceParameter = Expression.Parameter(typeof(TService), "instance");
        MethodInfo? GetService = typeof(IServiceProvider)
            .GetMethod(nameof(IServiceProvider.GetService));

        if (GetService == null)
        {
            throw new MissingMethodException(nameof(IServiceProvider), nameof(IServiceProvider.GetService));
        }

        ConstructorInfo constructor = implementationType.GetConstructors().First();
        IEnumerable<Expression> arguments = constructor.GetParameters()
            .Select(p =>
            {
                if (p.ParameterType == typeof(TService))
                {
                    return (Expression)instanceParameter;
                }
                return Expression.Convert(
                    Expression.Call(
                        serviceProviderParameter,
                        GetService,
                        Expression.Constant(p.ParameterType)),
                    p.ParameterType);
            });

        Expression constructionExpression = Expression.New(constructor, arguments);

        return Expression.Lambda<ServiceCreationWrapper<TService>>(
            constructionExpression,
            serviceProviderParameter,
            instanceParameter).Compile();
    }

    private static readonly MethodInfo GetTypeFromHandle =
        typeof(Type).GetMethod(nameof(Type.GetTypeFromHandle), BindingFlags.Static | BindingFlags.Public)!;
    private static readonly MethodInfo GetService =
        typeof(IServiceProvider).GetMethod(nameof(IServiceProvider.GetService), BindingFlags.Instance | BindingFlags.Public)!;
    private static ServiceInitializer? TryCreateMemberInjector(Type implementationType)
    {
        FieldInfo[] fields = implementationType
            .GetFields(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public)
            .Where(f => f.IsDefined(typeof(InjectAttribute), true) && !f.FieldType.IsValueType)
            .ToArray();

        PropertyInfo[] props = implementationType
            .GetProperties(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public)
            .Where(p => p.IsDefined(typeof(InjectAttribute), true) && p is { CanWrite: true, PropertyType.IsValueType: false })
            .ToArray();

        if (fields.Length == 0 && props.Length == 0)
            return null;

        DynamicMethod method = new DynamicMethod(
            $"Inject_{implementationType.Name}",
            null,
            [typeof(IServiceProvider), typeof(object)],
            implementationType.Module,
            skipVisibility: true
        );

        ILGenerator il = method.GetILGenerator();
        LocalBuilder typedTarget = il.DeclareLocal(implementationType);

        il.Emit(OpCodes.Ldarg_1);
        il.Emit(OpCodes.Castclass, implementationType);
        il.Emit(OpCodes.Stloc, typedTarget);

        foreach (FieldInfo field in fields)
        {
            il.Emit(OpCodes.Ldloc, typedTarget);
            il.Emit(OpCodes.Ldarg_0);
            il.Emit(OpCodes.Ldtoken, field.FieldType);
            il.Emit(OpCodes.Call, GetTypeFromHandle);
            il.Emit(OpCodes.Callvirt, GetService);
            il.Emit(OpCodes.Castclass, field.FieldType);
            il.Emit(OpCodes.Stfld, field);
        }

        foreach (PropertyInfo prop in props)
        {
            MethodInfo? setMethod = prop.GetSetMethod(true);
            if (setMethod == null) continue;

            il.Emit(OpCodes.Ldloc, typedTarget);
            il.Emit(OpCodes.Ldarg_0);
            il.Emit(OpCodes.Ldtoken, prop.PropertyType);
            il.Emit(OpCodes.Call, GetTypeFromHandle);
            il.Emit(OpCodes.Callvirt, GetService);
            il.Emit(OpCodes.Castclass, prop.PropertyType);
            il.Emit(OpCodes.Callvirt, setMethod);
        }

        il.Emit(OpCodes.Ret);
        return method.CreateDelegate<ServiceInitializer>();
    }
}
