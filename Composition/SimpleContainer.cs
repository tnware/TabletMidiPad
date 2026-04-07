using System;
using System.Collections.Generic;

namespace TabletMidiPad.Composition;

/// <summary>
/// Minimal DI container for this application.
/// </summary>
/// <remarks>
/// This intentionally avoids bringing in external DI packages for a small app.
/// It supports singleton and transient registrations and resolves by type.
/// </remarks>
internal sealed class SimpleContainer : IServiceProvider
{
    private readonly Dictionary<Type, Registration> _registrations = new();
    private readonly Dictionary<Type, object> _singletons = new();

    private readonly record struct Registration(
        Func<SimpleContainer, object> Factory,
        bool IsSingleton);

    public void AddSingleton<TService>(Func<SimpleContainer, TService> factory)
        where TService : notnull
    {
        ArgumentNullException.ThrowIfNull(factory);
        _registrations[typeof(TService)] = new Registration(c => factory(c), IsSingleton: true);
    }

    public void AddSingleton<TService, TImpl>()
        where TService : class
        where TImpl : class, TService, new()
    {
        AddSingleton<TService>(_ => new TImpl());
    }

    public void AddTransient<TService>(Func<SimpleContainer, TService> factory)
        where TService : notnull
    {
        ArgumentNullException.ThrowIfNull(factory);
        _registrations[typeof(TService)] = new Registration(c => factory(c), IsSingleton: false);
    }

    public void AddTransient<TService, TImpl>()
        where TService : class
        where TImpl : class, TService, new()
    {
        AddTransient<TService>(_ => new TImpl());
    }

    public T GetRequiredService<T>()
        where T : notnull
    {
        return (T)(GetService(typeof(T)) ?? throw new InvalidOperationException($"Service not registered: {typeof(T).FullName}"));
    }

    public object? GetService(Type serviceType)
    {
        if (_singletons.TryGetValue(serviceType, out object? existing))
        {
            return existing;
        }

        if (!_registrations.TryGetValue(serviceType, out Registration registration))
        {
            return null;
        }

        object created = registration.Factory(this);

        if (registration.IsSingleton)
        {
            _singletons[serviceType] = created;
        }

        return created;
    }
}

