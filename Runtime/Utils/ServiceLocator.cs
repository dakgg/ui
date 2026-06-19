using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.SceneManagement;

public class ServiceLocator : MonoBehaviour, IServiceLocator, IServiceLocatorBuilder
{
    static readonly List<IServiceLocator> s_Scopes = new();
    static readonly List<Action<IServiceLocatorBuilder>> s_Registers = new();

    public class ParentScope : IDisposable
    {
        readonly IServiceLocator m_BoundScope;

        public ParentScope(IServiceLocator binder)
        {
            m_BoundScope = binder;
            s_Scopes.Add(m_BoundScope);
        }

        public void Dispose()
        {
            if (m_BoundScope != null)
            {
                s_Scopes.Remove(m_BoundScope);
            }
        }
    }

    public class RegisterScope : IDisposable
    {
        readonly Action<IServiceLocatorBuilder> m_RegisterAction;

        public RegisterScope(Action<IServiceLocatorBuilder> builder)
        {
            m_RegisterAction = builder;
            s_Registers.Add(m_RegisterAction);
        }

        public void Dispose()
        {
            if (m_RegisterAction != null)
            {
                s_Registers.Remove(m_RegisterAction);
            }
        }
    }

    public static ParentScope EnqueueParent(IServiceLocator parent) => new ParentScope(parent);

    public static IServiceLocator GetParentFromScope() => s_Scopes.Count > 0 ? s_Scopes[^1] : null;

    static ServiceLocator s_GlobalServiceLocator;

    public static void SetGlobalServiceLocator(ServiceLocator service) => s_GlobalServiceLocator = service;

    protected bool m_IsBuilt = false;

    readonly HashSet<object> m_Objects = new();
    readonly HashSet<IServiceLocatorSetupComponent> m_SetupComponent = new();
    readonly HashSet<IPostInjectListener> m_PostInjectListeners = new();
    readonly HashSet<IStartableService> m_StartableServices = new();
    readonly HashSet<IUpdatableService> m_UpdatableServices = new();
    readonly HashSet<IDisposableService> m_DisposableServices = new();

    public Dictionary<System.Type, object> m_Services = new Dictionary<System.Type, object>();

    protected IServiceLocator m_ParentScope;

    protected virtual void Awake()
    {
        m_ParentScope = GetParentFromScope();
        Configure();
    }

    protected void Configure()
    {
        Register<IServiceLocator>(this);
        CollectServiceComponents();
        m_IsBuilt = true;
    }
    
    
    protected static List<IServiceLocatorComponentBase> CollectChildInterfaces(GameObject go)
    {
        var result = new List<IServiceLocatorComponentBase>();
            
        _CollectRecursive(go, result);
            
        return result;

        static void _CollectRecursive(GameObject go, List<IServiceLocatorComponentBase> result)
        {
            var injector = go.GetComponent<ServiceLocator>(); 
            
            //other injector found
            if(injector != null && injector.gameObject != go) return;
            result.AddRange(go.GetComponents<IServiceLocatorComponentBase>());

            for (var i = 0; i < go.transform.childCount; i++)
            {
                var child = go.transform.GetChild(i);
                _CollectRecursive(child.gameObject, result);
            }
        }
    }

    public void Register(System.Type type, object deps)
    {
        if (m_IsBuilt) throw new System.Exception("ServiceLocator is already built");
        if (!m_Services.TryAdd(type, deps)) throw new System.Exception($"ServiceLocator of same type already exist {type}");
        m_Objects.Add(deps);
    }

    public void Register<T>(T deps)
    {
        if (m_IsBuilt) throw new System.Exception("ServiceLocator is already built");
        var type = typeof(T);
        if (!m_Services.TryAdd(type, deps)) throw new System.Exception($"ServiceLocator of same type already exist {type}");
        m_Objects.Add(deps);
    }

    public T Resolve<T>()
    {
        var type = typeof(T);
        if (TryResolve<T>(out var found)) return found;
        throw new System.Exception($"Dependency not found for type {type}");
    }

    public bool TryResolve(Type type, out object value)
    {
        value = default;

        {
            if (m_Services.TryGetValue(type, out var found))
            {
                value = found;
                return true;
            }
        }

        {
            if (m_ParentScope != null && !ReferenceEquals(m_ParentScope, this) && m_ParentScope.TryResolve(type, out var found))
            {
                value = found;
                return true;
            }
        }

        {
            if (this is not ServiceLocatorScene &&
                ServiceLocatorScene.TryResolveFromScene(type, gameObject.scene, out var found))
            {
                value = found;
                return true;
            }
        }

        {
            if (this != s_GlobalServiceLocator && null != s_GlobalServiceLocator && s_GlobalServiceLocator.TryResolve(type, out var found))
            {
                value = found;
                return true;
            }
        }
        
        return false;
    }

    public bool TryResolve<T>(out T value)
    {
        if (!TryResolve(typeof(T), out var valueObject))
        {
            value = default;
            return false;
        }

        value = (T)valueObject;
        return true;
    }

    public static T ResolveStatic<T>()
    {
        var type = typeof(T);
        if (TryResolveStatic<T>(out var found)) return found;
        throw new System.Exception($"Dependency not found for type {type}");
    }

    public static bool TryResolveStatic<T>(out T value)
    {
        return ServiceLocatorScene.TryResolveFromScene(SceneManager.GetSceneAt(0), out value);
    }
    
    protected virtual IEnumerable<IServiceLocatorComponentBase> CollectService()
    {
        return CollectChildInterfaces(gameObject);
    }
    

    public void CollectServiceComponents()
    {
        foreach (var depsComponent in CollectService())
        {
            if (depsComponent is IServiceLocatorComponent comp) comp.OnRegister(this);
            m_Objects.Add(depsComponent);
        }
        
        //register scoped
        foreach (var register in s_Registers)
        {
            register.Invoke(this);
        }

        //Collects other dependencyProvideres that are not included in the objects array
        OnAdditionalSetupService();

        foreach (var obj in m_Objects)
        {
            if (obj is IServiceLocatorSetupComponent setup) m_SetupComponent.Add(setup);
            if (obj is IStartableService startable) m_StartableServices.Add(startable);
            if (obj is IPostInjectListener postInject) m_PostInjectListeners.Add(postInject);
            if (obj is IUpdatableService updatable) m_UpdatableServices.Add(updatable);
            if (obj is IDisposableService disposable) m_DisposableServices.Add(disposable);
        }
        
        //inject
        foreach(var setup in m_SetupComponent) Inject(setup);
        
        //post inject
        foreach(var setup in m_PostInjectListeners) setup.OnPostInject();
    }

    public class ReflectionCache
    {
        static Dictionary<Type, ReflectionCache> s_Cache = new();

        public static ReflectionCache GetCache(Type type)
        {
            if (!s_Cache.TryGetValue(type, out var found))
            {
                found = new ReflectionCache(type);
                s_Cache.Add(type, found);
            }

            return found;
        }

        private ReflectionCache(Type type)
        {
            {
                var fields = type.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                List<(FieldInfo, InjectAttribute)> attrs = new();
                foreach (var field in fields)
                {
                    var attr = field.GetCustomAttribute<InjectAttribute>();
                    if (attr == null) continue;
                    attrs.Add((field, attr));
                }

                FieldAttributes = attrs;
            }
            {
                var properties = type.GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                List<(PropertyInfo, InjectAttribute)> attrs = new();
                foreach (var property in properties)
                {
                    var attr = property.GetCustomAttribute<InjectAttribute>();
                    if (attr == null) continue;
                    attrs.Add((property, attr));
                }

                PropertyAttributes = attrs;
            }
        }

        public readonly IReadOnlyList<(FieldInfo, InjectAttribute)> FieldAttributes;
        public readonly IReadOnlyList<(PropertyInfo, InjectAttribute)> PropertyAttributes;
    }

    private void Start()
    {
        foreach (var startable in m_StartableServices)
        {
            startable.StartService();
        }

        m_StartableServices.Clear();
    }

    private void Update()
    {
        foreach (var updatable in m_UpdatableServices)
        {
            updatable.UpdateService();
        }
    }

    void OnDestroy()
    {
        foreach (var disposable in m_DisposableServices)
        {
            disposable.DisposeService();
        }
    }

    protected virtual void OnAdditionalSetupService() { }

    public void Inject(IServiceLocatorSetupComponent setupComponent)
    {
        setupComponent.SetupServiceLocator(this);

        var cache = ReflectionCache.GetCache(setupComponent.GetType());
        foreach (var (fi, attr) in cache.FieldAttributes)
        {
            if (TryResolve(fi.FieldType, out var found))
            {
                fi.SetValue(setupComponent, found);
            }
            else if (attr.Required)
            {
                throw new Exception($"{setupComponent.GetType().Name} fi.FieldType : {fi.FieldType}");
            }
        }

        foreach (var (pi, attr) in cache.PropertyAttributes)
        {
            if (TryResolve(pi.PropertyType, out var found))
            {
                pi.SetValue(setupComponent, found);
            }
            else if (attr.Required)
            {
                throw new Exception($"fi.FieldType : {pi.PropertyType}");
            }
        }
    }

    public void SetAccessor<T>()
    {     
        if(TryResolve(out T value) && TryResolve(out Accessor<T> accessor))
        {
            accessor.SetData(value);
        }
        else
        {
            var type = typeof(Accessor<T>);
            throw new System.Exception($"Dependency not found for type {type}");
        }
    }
}

public interface IServiceLocatorComponentBase { }

public interface IServiceLocator
{
    T Resolve<T>();
    bool TryResolve<T>(out T value);
    bool TryResolve(Type type, out object value);
    void Inject(IServiceLocatorSetupComponent setupComponent);
}

public interface IServiceLocatorBuilder
{
    void Register<T>(T builder);
    void Register(Type serviceType, object instance);
}

public interface IServiceLocatorComponent : IServiceLocatorComponentBase
{
    void OnRegister(IServiceLocatorBuilder builder)
    {
        builder.Register(GetType(), this);
    }
}

public interface IServiceLocatorSetupComponent : IServiceLocatorComponentBase
{
    void SetupServiceLocator(IServiceLocator service) { }
}

public interface IPostInjectListener : IServiceLocatorComponentBase
{
    void OnPostInject();
}

public interface IStartableService : IServiceLocatorComponentBase
{
    void StartService();
}

public interface IUpdatableService : IServiceLocatorComponentBase
{
    void UpdateService();
}

public interface IDisposableService : IServiceLocatorComponentBase
{
    void DisposeService();
}

[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field)]
public class InjectAttribute : Attribute
{
    public bool Required { get; private set; }

    public InjectAttribute(bool required = true)
    {
        Required = required;
    }
}