using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace NeXt.DependsOnNestedProperty
{
    /// <summary>
    /// Marks a property for deep nested change tracking
    /// </summary>
    [AttributeUsage(AttributeTargets.Property, AllowMultiple = true)]
    public sealed class DependsOnNestedAttribute : Attribute 
    {
        /// <summary>
        /// Marks a property for deep nested change tracking
        /// </summary>
        /// <param name="path">the full dot seperated property path (validity is checked by analyzer if enabled)</param>
        public DependsOnNestedAttribute(string path)
        {
          Path = new ReadOnlyCollection<string>(path.Split('.').ToList());  
        } 

        /// <summary>
        /// The property path list for the nested dependency
        /// </summary>
        public IReadOnlyList<string> Path { get; }
    }
}
