using System.Collections.Concurrent;
using System.Reflection;
using ShipMvp.Core.Attributes;

namespace ShipMvp.Core.Modules
{
    public class ModuleContainer
    {
        private static readonly Lazy<ModuleContainer> _instance = new(() => new ModuleContainer());
        private readonly ConcurrentDictionary<Type, IModule> _moduleInstances = new();
        private readonly HashSet<Type> _visitedTypes = new();
        private readonly HashSet<Type> _currentlyProcessing = new();

        public static ModuleContainer Instance => _instance.Value;

        private ModuleContainer() { }

        public IModule GetOrCreateModule(Type moduleType)
        {
            return _moduleInstances.GetOrAdd(moduleType, type =>
            {
                // Check for circular dependencies
                if (_currentlyProcessing.Contains(type))
                {
                    throw new InvalidOperationException($"Circular dependency detected for module: {type.Name}");
                }

                _currentlyProcessing.Add(type);

                try
                {
                    // Recursively resolve dependencies first
                    ResolveDependencies(type);

                    // Create the module instance
                    var module = (IModule)Activator.CreateInstance(type)!;

                    _visitedTypes.Add(type);
                    return module;
                }
                finally
                {
                    _currentlyProcessing.Remove(type);
                }
            });
        }

        private void ResolveDependencies(Type moduleType)
        {
            // Get all DependsOn attributes
            var allAttributes = moduleType.GetCustomAttributes(true);
            foreach (var attr in allAttributes)
            {
                var attrType = attr.GetType();
                if (attrType.IsGenericType && 
                    attrType.GetGenericTypeDefinition() == typeof(DependsOnAttribute<>))
                {
                    // Get the dependency type from the generic argument
                    var dependencyType = attrType.GetGenericArguments().FirstOrDefault();
                    if (dependencyType != null)
                    {
                        // Recursively create dependency modules
                        GetOrCreateModule(dependencyType);
                    }
                }
            }
        }

        public IEnumerable<IModule> GetAllModules(IEnumerable<Type> moduleTypes)
        {
            // Process all provided module types to ensure their dependencies are loaded
            foreach (var moduleType in moduleTypes)
            {
                GetOrCreateModule(moduleType);
            }

            // Return modules in topological order (dependencies first)
            return GetModulesInTopologicalOrder();
        }

        private IEnumerable<IModule> GetModulesInTopologicalOrder()
        {
            var sortedModules = new List<IModule>();
            var visited = new HashSet<Type>();
            var visiting = new HashSet<Type>();

            foreach (var moduleType in _moduleInstances.Keys)
            {
                if (!visited.Contains(moduleType))
                {
                    TopologicalSort(moduleType, visited, visiting, sortedModules);
                }
            }

            return sortedModules;
        }

        private void TopologicalSort(Type moduleType, HashSet<Type> visited, HashSet<Type> visiting, List<IModule> result)
        {
            if (visiting.Contains(moduleType))
            {
                throw new InvalidOperationException($"Circular dependency detected involving module: {moduleType.Name}");
            }

            if (visited.Contains(moduleType))
            {
                return;
            }

            visiting.Add(moduleType);

            // Visit dependencies first
            var allAttributes = moduleType.GetCustomAttributes(true);
            foreach (var attr in allAttributes)
            {
                var attrType = attr.GetType();
                if (attrType.IsGenericType && 
                    attrType.GetGenericTypeDefinition() == typeof(DependsOnAttribute<>))
                {
                    var dependencyType = attrType.GetGenericArguments().FirstOrDefault();
                    if (dependencyType != null && _moduleInstances.ContainsKey(dependencyType))
                    {
                        TopologicalSort(dependencyType, visited, visiting, result);
                    }
                }
            }

            visiting.Remove(moduleType);
            visited.Add(moduleType);

            // Add current module to result
            if (_moduleInstances.TryGetValue(moduleType, out var module))
            {
                result.Add(module);
            }
        }
    }
}
