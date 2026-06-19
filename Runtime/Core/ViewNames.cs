using System;
using System.Collections.Generic;
using System.Reflection;

namespace ViewSystem
{
    public static class ViewNames
    {
        private static readonly Dictionary<Type, string> s_ViewNames = new();
        
        public static string GetName(Type type)
        {
            if (!s_ViewNames.TryGetValue(type, out var result))
            {
                result = GetLoadPath(type);
                s_ViewNames.Add(type, result);
            }

            return result;
        }

        public static string GetName(this ViewBase view)
        {
            return GetName(view.GetType());
        }

        private static string GetLoadPath(Type type)
        {
            var attribute = type.GetCustomAttribute<ViewLoadAttribute>();
            string loadPath = attribute == null ? type.Name : attribute.LoadPath;
            return $"{loadPath}@View";
        }
    }
}
