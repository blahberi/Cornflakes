# Cornflakes
Cornflakes is a lightweight, blazingly fast Dependency Injection framework for .NET written in C#.
<br>
# Usage
## Defining Services
A **Service** refers to an interface which is implemented by a class, which we call the **Service Implementation**. A service implementation can depend on other services by requiring their type through the constructor.

```csharp
// Service Type
interface IFoo
{
    void FooMethod();
}

// Another service type
interface IBar
{
    int BarMethod();
}

// Service Implementation for type IFoo
class Foo : IFoo
{
    private readonly IBar bar;
    public Foo(IBar bar) // Depends on service with type IBar
    {
        this.bar = bar;
        Console.WriteLine($"Foo has been initialized: {this.GetHashCode()}");
    }

    public void FooMethod()
    {
        Console.WriteLine($"Foo method is called: ${this.GetHashCode()}")
        // Calling method of injected service
        int res = this.bar.BarMethod(5);
        Console.WriteLine($"Foo calls Bar which returns {res}: {this.GetHashCode()}");
    }
}
```
In the example above, the service is `IFoo`, and the implementation is `Foo`, which depends on the service `IBar`.

## Lifetime Strategies
**Lifetime Strategies** define how instances of a service should be created when requested. Cornflakes comes with a couple of built-in lifetime strategies.

### Transient
Creates a new instance of the service every time it is requested.

### Singleton
Creates only a single instance of the service, and returns that same single instance every time it is requested.

### Scoped
Is similar to the Singleton lifetime strategy. However, instead of having a single global instance, every **Scope** (which will be discussed later) has its own unique instance.

We can also define custom creation startegies, more on that later.

## Register Services
Service registration is done through the `ServiceProviderBuilder`.

```csharp
IServiceProvider serviceProvider = new ServiceProviderBuilder()
    .RegisterSingleton<IFoo, Foo>()
    .RegisterTransient<IBar, Bar>()
    .RegisterScoped<IBaz, Baz>()
    .Build();
```
When registering a service, we specify the **Service**, **Service Implementation**, and **lifetime Strategy**.

In the example above, we registered the following services:

| Service | Service Implementation | lifetime strategy          |
|---------|------------------------|----------------------------|
| `IFoo`  | `Foo`                  | Singleton                  |
| `IBar`  | `Bar`                  | Transient                  |
| `IBaz`  | `Baz`                  | Scoped                     |

## Requesting services
Once our services have been registered through `ServiceProviderBuilder`, we can finalize the service registration process and create the `ServiceProvider` using the `Build()` method.

In order to request a service, we need to specify the the service we are requesting.

```csharp
// Request the service with type IFoo
IFoo fooService = serviceProvider.GetService<IFoo>()
fooService.FooMethod();
```
Notice how the framework automatically resolves the dependencies of the service implementation.

**note:** The service being requested, all of its dependencies, and all of their dependencies (and so on, recursively), need to be registered. Thus in this example, the service with `IFoo` needs to be registered. And if the service implemenetation depends on the service `IBar`, then it needs to be registered as well.

## Scopes
Cornflakes provides a **Scope** system which allows for more granular control over the lifetime of instances of a service. Within a **Scope**, Scoped services (services that use the Scoped lifetime strategy) are instantiated once. Each scope has its own single instance of the Scoped service.

### Using Scopes
We can create a scope using the service provider's `CreateScope()` method.

In the following example, assume that the service `IFoo` is Scoped.
```csharp
using (IScope scope = serviceProvider.CreateScope()) 
{
    // Get the scope's service provider.
    IServiceProvider scopedProvider = scope.ServiceProvider;

    // Request the service with type IFoo from within the scope.
    IFoo fooService1 = scopedProvider.GetService<IFoo>();
    IFoo fooService2 = scopedProvider.GetService<IFoo>();

    // Since fooService1 and fooService2 are requested within the same scope,
    // they will both point to the same instance (let's call its hash code 'A')
    Console.WriteLine(fooService1.GetHashCode()); // Outputs A
    Console.WriteLine(fooService2.GetHashCode()); // Outputs A
}

using (IScope scope = serviceProvider.CreateScope()) 
{
    // Get the scope's service provider.
    IServiceProvider scopedProvider = scope.ServiceProvider;

    // Request the service with type IFoo from within a different scope.
    IFoo fooService3 = scopedProvider.GetService<IFoo>();
    IFoo fooService4 = scopedProvider.GetService<IFoo>();

    // Since this is a different scope, fooService3 and fooService4 will
    // point to a new instance (with a different hash code, 'B').
    Console.WriteLine(fooService3.GetHashCode()); // Outputs B
    Console.WriteLine(fooService4.GetHashCode()); // Outputs B
}
```

In the example above, we can see that when we request the same Scoped service twice within a scope, we get the same instance. However, if we request that service from a different scope, we get a different instance.

### Nested Scopes
Scoped can be nested. Each scope will have its own unique instances of Scoped Services.

```csharp
using (IScope outerScope = serviceProvider.CreateScope())
{
    Foo outerFoo = outerScope.ServiceProvider.GetService<IFoo>();

    using (IScope innerScope = outerScope.ServiceProvider.CreateScope())
    {
        IFoo innerFoo = innerScope.ServiceProvider.GetService<IFoo>();

        // outerFoo and innerFoo are different instances
        Console.WriteLine(outerFoo.GetHashCode()); // Outputs A
        Console.WriteLine(innerFoo.GetHashCode()); // Outputs B
    }
}
```

## Service Factory Methods
**Service Factory Methods** are methods that create a new instance of a service. They are called by the lifetime strategy whenever it creates a new instance of a service. Cornflakes provides a default service factory method which is used when a custom factory method is not provided when registering a service.


### Custom Service Factory Methods
We can define custom factory methods, which can be useful when we need to perform some custom logic when creating a service instance. Custom factory methods are defined when registering a service, and they are defined as delegates that take an `IServiceProvider` as an argument and return an instance of the service.

```csharp
IServiceProvider serviceProvider = new ServiceProviderBuilder()
    .RegisterSingleton<IFoo, Foo>() // Default factory method
    .RegisterTransient<IBar>(serviceProvider => {
        // Custom factory method
        return new Bar();
    }) 
    .RegisterScoped<IBaz, Baz>()
    .Build();
```

If a service implementation depends on another service and we are using a custom factory method for that service, we have to resolve the dependencies ourselves within the factory method.
```csharp
IServiceProvider serviceProvider = new ServiceProviderBuilder()
    .RegisterSingleton<IFoo, Foo>() // Default factory method
    .RegisterTransient<IBar>(serviceProvider => {
        // Custom factory method
        IFoo foo = serviceProvider.GetService<IFoo>();
        return new Bar(foo); // Resolve the dependency (Bar depends on IFoo)
    }) 
    .RegisterScoped<IBaz, Baz>()
    .Build();
```

### Default Service Factory Method
The default service factory method simply creates a new instance of the service and resolves its dependencies. Cornflakes will use the default service factory method if a custom factory method is not provided when registering a service. Cornflakes uses code generation to create the default service factory method, which provides high performance service instantiation and dependency resolution.
For example, if we have the follwing service registration:
```csharp
IServiceProvider serviceProvider = new ServiceProviderBuilder()
    .RegisterSingleton<IFoo, Foo>() // Assume Foo depends on IBar and IBaz
    .RegisterTransient<IBar, Bar>() // Assume Bar depends on IBaz
    .RegisterScoped<IBaz, Baz>() // Assume Baz has no dependencies
    .Build();
```
Then the default service factory methods generated by Cornflakes would look like this:

```csharp
// IFoo Default Service Factory Method
(IServiceProvider serviceProvider) => {
    return new Foo(serviceProvider.GetService<IBar>(), serviceProvider.GetService<IBaz>());
}

// IBar Default Service Factory Method
(IServiceProvider serviceProvider) => {
    return new Bar(serviceProvider.GetService<IBaz>());
}

// IBaz Default Service Factory Method
(IServiceProvider serviceProvider) => {
    return new Baz();
}
```

The default service factory method can be retrieved by using the `DefaultServiceFactory.GetServiceFactory<TImplementation>()` method where `TImplementation` is the service implementation.
We can even use the default service factory method as a custom factory method. In fact,
```csharp
IServiceProvider serviceProvider = new ServiceProviderBuilder()
    .RegisterSingleton<IFoo, Foo>() // Default factory method
    .Build();
```
is completely equivalent to
```csharp
IServiceProvider serviceProvider = new ServiceProviderBuilder()
    // Passing the default factory method as a custom factory method
    .RegisterSingleton<IFoo>(DefaultServiceFactory.GetServiceFactory<Foo>())
    .Build();
```
Although the former is cleaner, less verbose, and more concise than the latter, which is why it is recommended.

## Custom Lifetime Strategies
Custom lifetime strategies can be useful when we need to perform some custom logic for handling the lifetime of service instances. We can define custom lifetime strategies by implementing the `ILifetimeStrategy` interface. As an example, we will implement the `Transient` and `Singleton` lifetime strategies.

### Transient lifetime Strategy Implementation
```csharp
class CustomTransientCreation: ILifetimeStrategy
{
    private readonly ServiceFactory serviceFactory;

    public TransientCreation(ServiceFactory serviceFactory) 
    {
        this.serviceFactory = serviceFactory;
    }

    public object GetInstance(IServiceProvider serviceProvider)
    {
        // Custom logic to create a service instance
        return this.serviceFactory(serviceProvider);
    }
}
```
It is good practice to decouple the instantitation logic from the lifetime strategy by requiring a `ServiceFactory` delegate in the constructor, which allows for custom service factory methods.

Now we can register services using our custom lifetime strategy. For the examples below, we will assume that `Bar` depends on `IFoo`.
```csharp
IServiceProvider serviceProvider = new ServiceProviderBuilder()
    .RegisterService<IFoo, Foo>(new CustomTransientCreation(
        DefaultServiceFactory.GetServiceFactory<Foo>())) // Default factory method
    .RegisterService<IBar, Bar>(new CustomTransientCreation((serviceProvider) => {
        // Custom factory method
        IFoo foo = serviceProvider.GetService<IFoo>();
        return new Bar(foo);
    }))
    .Build();
```

### Singleton lifetime Strategy Implementation
```csharp
class CustomSingletonCreation: ILifetimeStrategy
{
    private readonly ServiceFactory serviceFactory;
    private object instance;
    public SingletonCreation(ServiceFactory serviceFactory) 
    {
        this.serviceFactory = serviceFactory;
    }

    public object GetInstance(IServiceProvider serviceProvider)
    {
        if (this.instance == null)
        {
            this.instance = this.serviceFactory(serviceProvider);
        }
        return this.instance;
    }
}
```
Then service registation is performed as follows:
```csharp
IServiceProvider serviceProvider = new ServiceProviderBuilder()
    .RegisterService<IFoo>(new CustomSingletonCreation(
        DefaultServiceFactory.GetServiceFactory<Foo>())) // Default factory method
    .RegisterService<IBar>(new CustomSingletonCreation((serviceProvider) => {
        // Custom factory method
        IFoo foo = serviceProvider.GetService<IFoo>();
        return new Bar(foo);
    }))
    .Build();
```

### Extending the Builder
Notice that currently, registering services with our custom lifetime strategies is a bit verbose, unlike the built-in lifetime strategies. We can extend the `ServiceProviderBuilder` to provide a more fluent API for registering services with custom lifetime strategies.

```csharp
public static class CustomServiceProvideBuilderExtensions
{
    public static IServiceProviderBuilder RegisterCustomTransient<TService>(
        this IServiceProviderBuilder builder, ServiceFactory serviceFactory)
    {
        return builder.RegisterService<TService>(new CustomTransientCreation(serviceFactory));
    }

    public static IServiceProviderBuilder RegisterCustomSingleton<TService>(
        this IServiceProviderBuilder builder, ServiceFactory serviceFactory)
    {
        return builder.RegisterService<TService>(new CustomSingletonCreation(serviceFactory));
    }

    // Extension methods for using the default service factory method
    public static IServiceProviderBuilder RegisterCustomTransient<TService, TImplementation>(
        this IServiceProviderBuilder builder)
    {
        return builder.RegisterService<TService>(new CustomTransientCreation(
            DefaultServiceFactory.GetServiceFactory<TImplementation>()));
        ));
    }

    public static IServiceProviderBuilder RegisterCustomSingleton<TService, TImplementation>(
        this IServiceProviderBuilder builder)
    {
        return builder.RegisterService<TService>(new CustomSingletonCreation(
            DefaultServiceFactory.GetServiceFactory<TImplementation>()));
    }
}
```
Now the service registration process is much more fluent and streamlined.
```csharp
IServiceProvider serviceProvider = new ServiceProviderBuilder()
    .RegisterCustomSingleton<IFoo, Foo>() // Default factory method
    .RegisterCustomTransient<IBar>((serviceProvider) => {
        // Custom factory method
        IFoo foo = serviceProvider.GetService<IFoo>();
        return new Bar(foo);
    })
    .Build();
```