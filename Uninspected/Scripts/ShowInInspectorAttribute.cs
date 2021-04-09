using System;

namespace Uninspected
{
    // Could be split into InspectProperty & InspectMethod, as the writable doesn't overlap
    [AttributeUsage(AttributeTargets.Property | AttributeTargets.Method)]
    public sealed class ShowInInspectorAttribute : Attribute
    {
        /// <summary>
        /// If attached to a property, determines when the property is writable.
        /// </summary>
        public Writable Writable { get; }

        /// <summary>
        /// When is the property available, editor, runtime etc.
        /// </summary>
        public Availability Availability { get; }
        /// <summary>
        /// If the game object is a prefab or a prefab instance, is the property then available?
        /// </summary>
        public PrefabAvailability PrefabAvailability { get; }

        public ShowInInspectorAttribute()
        {
            Availability = Availability.Inherit;
            Writable = Writable.RuntimeOnly;
            PrefabAvailability = PrefabAvailability.Never;
        }

        public ShowInInspectorAttribute(
            Availability availability = Availability.Inherit,
            Writable writable = Writable.RuntimeOnly,
            PrefabAvailability prefabAvailability = PrefabAvailability.Never
        )
        {
            Writable = writable;
            Availability = availability;
            PrefabAvailability = prefabAvailability;
        }
        
        public ShowInInspectorAttribute(
            Writable writable = Writable.RuntimeOnly,
            Availability availability = Availability.Inherit,
            PrefabAvailability prefabAvailability = PrefabAvailability.Never
        )
        {
            Writable = writable;
            Availability = availability;
            PrefabAvailability = prefabAvailability;
        }
    }

    public enum Writable
    {
        /// <summary>
        /// The element is never editable and always drawn as disabled.
        /// </summary>
        Never,
        
        /// <summary>
        /// The element is only editable in runtime.
        /// </summary>
        RuntimeOnly,
        
        /// <summary>
        /// The element is only editable outside of runtime.
        /// </summary>
        EditorOnly,
        
        /// <summary>
        /// Element is always editable, both in runtime and editor.
        /// </summary>
        Always
    }

    public enum Availability
    {
        /// <summary>
        /// Property is only enabled during runtime,
        /// unless the parent class has [ExecuteAlways] then it is also enabled in the editor.
        /// </summary>
        Inherit,

        /// <summary>
        /// The property is always enabled.
        /// </summary>
        Always,

        /// <summary>
        /// The property is only enabled while playing.
        /// </summary>
        RuntimeOnly
    }

    public enum PrefabAvailability
    {
        /// <summary>
        /// Property is enabled while editing a prefab instance
        /// </summary>
        InstanceOnly,

        /// <summary>
        /// Property is enabled while editing a prefab asset
        /// </summary>
        AssetOnly,

        /// <summary>
        /// Property is enabled while editing a prefab instance or asset
        /// </summary>
        Always,

        /// <summary>
        /// Property is never enabled while editing a prefab
        /// </summary>
        Never
    }
}