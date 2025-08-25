using System.Reflection;
using ShipMvp.Core.Attributes;

namespace ShipMvp.Core.Modules;

/// <summary>
/// Registry to manage module instances and ensure no double instantiation
/// </summary>
public class ModuleRegistry
{
    private readonly Dictionary<Type, IModule> _moduleInstances = new();
    private readonly IEnumerable<IModule> _allModules;

    public ModuleRegistry(IEnumerable<IModule> modules)
    {
        _allModules = modules;
    }

    public IModule GetOrCreateModule(Type moduleType)
    {
        if (_moduleInstances.ContainsKey(moduleType))
        {
            return _moduleInstances[moduleType];
        }

        // Instantiate the module
        var module = (IModule)Activator.CreateInstance(moduleType)!;

        // Resolve dependencies
        var dependsOnAttributes = moduleType.GetCustomAttributes<DependsOnAttribute<IModule>>();
        foreach (var attr in dependsOnAttributes)
        {
            var dependencyType = attr.GetType().GetGenericArguments().FirstOrDefault();
            if (dependencyType != null)
            {
                GetOrCreateModule(dependencyType);
            }
        }

        _moduleInstances[moduleType] = module;
        return module;
    }

    public IEnumerable<IModule> GetAllModules()
    {
        foreach (var module in _allModules)
        {
            GetOrCreateModule(module.GetType());
        }

        return _moduleInstances.Values;
    }
}
