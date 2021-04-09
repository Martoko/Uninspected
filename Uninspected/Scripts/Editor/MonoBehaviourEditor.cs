using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Uninspected.Editor
{
    [CanEditMultipleObjects]
    [CustomEditor(typeof(MonoBehaviour), true)]
    public class MonoBehaviourEditor : UnityEditor.Editor
    {
        private static readonly List<IDrawer> drawers = new List<IDrawer>
        {
            new Drawer<float>((fieldName, f) => EditorGUILayout.FloatField(fieldName, f)),
            new Drawer<int>((fieldName, f) => EditorGUILayout.IntField(fieldName, f)),
            new Drawer<long>((fieldName, f) => EditorGUILayout.LongField(fieldName, f)),
            new Drawer<bool>((fieldName, f) => EditorGUILayout.Toggle(fieldName, f)),
            new Drawer<string>((fieldName, f) => EditorGUILayout.TextField(fieldName, f)),
            new Drawer<Enum>((fieldName, f) => EditorGUILayout.EnumPopup(fieldName, f)),
            new Drawer<Color>((fieldName, f) => EditorGUILayout.ColorField(fieldName, f)),
            new Drawer<Vector2>((fieldName, f) => EditorGUILayout.Vector2Field(fieldName, f)),
            new Drawer<Vector3>((fieldName, f) => EditorGUILayout.Vector3Field(fieldName, f)),
            new Drawer<Quaternion>((fieldName, f) =>
                Quaternion.Euler(EditorGUILayout.Vector3Field(fieldName, f.eulerAngles))
            ),
            new Drawer<Vector4>((fieldName, f) => EditorGUILayout.Vector4Field(fieldName, f)),
            new Drawer<Pose>((fieldName, f) =>
                new Pose(
                    EditorGUILayout.Vector3Field(fieldName + " Position", f.position),
                    Quaternion.Euler(EditorGUILayout.Vector3Field(fieldName + " Rotation", f.rotation.eulerAngles))
                )
            ),
            new Drawer<Transform>((fieldName, f) =>
                (Transform) EditorGUILayout.ObjectField(fieldName, f, typeof(Transform), true)
            ),
            new Drawer<ScriptableObject>((fieldName, f) =>
                (ScriptableObject) EditorGUILayout.ObjectField(fieldName, f, typeof(UnityEngine.Object), true)
            )
        };

        private readonly IList<MethodCall> methodCalls = new List<MethodCall>();

        private readonly List<DisplayedProperty> displayedProperties = new List<DisplayedProperty>();

        private bool executeAlways;

        public void OnEnable()
        {
            if (target.GetType().GetCustomAttribute<ExecuteAlways>() != null) executeAlways = true;
            if (target.GetType().GetCustomAttribute<ExecuteInEditMode>() != null) executeAlways = true;

            var methods = target.GetType().GetMethods(
                BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic
            );
            foreach (var method in methods)
            {
                var customAttributes = method.GetCustomAttributes(typeof(ShowInInspectorAttribute), false);
                if (customAttributes.Any())
                {
                    var arguments = new List<Argument>();
                    foreach (var parameterInfo in method.GetParameters())
                    {
                        // Accept the default value or use the equivalent of default(TYPE)
                        var value = parameterInfo.HasDefaultValue
                            ? parameterInfo.DefaultValue // The parameter has a default value, use that
                            : parameterInfo.ParameterType.IsValueType // We don't have a default value
                                ? Activator.CreateInstance(parameterInfo.ParameterType) // Default for value types
                                : null; // Null for reference types

                        arguments.Add(new Argument(parameterInfo, value));
                    }

                    var enableProperty = ((ShowInInspectorAttribute) customAttributes.First()).Availability;
                    methodCalls.Add(new MethodCall(method, arguments, enableProperty));
                }
            }

            var properties = target.GetType().GetProperties(
                BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic
            );
            foreach (var property in properties)
            {
                var customAttributes = property.GetCustomAttributes(typeof(ShowInInspectorAttribute), false);
                if (customAttributes.Any())
                {
                    var showInInspectorAttribute = (ShowInInspectorAttribute) customAttributes.First();
                    displayedProperties.Add(new DisplayedProperty(
                        property,
                        showInInspectorAttribute.Writable,
                        showInInspectorAttribute.Availability,
                        showInInspectorAttribute.PrefabAvailability
                    ));
                }
            }
        }

        public override void OnInspectorGUI()
        {
            base.OnInspectorGUI();

            if (displayedProperties.Count > 0)
            {
                foreach (var displayedProperty in displayedProperties)
                {
                    displayedProperty.IsOfUnsupportedType = true;
                    foreach (var drawer in drawers)
                    {
                        if (drawer.Supports(displayedProperty.Property.GetMethod.ReturnType))
                        {
                            var enableProperty = displayedProperty.Availability;
                            var enablePropertyInPrefab = displayedProperty.PrefabAvailability;
                            var mutability = displayedProperty.Mutability;
                            var isPlaying = targets.All(Application.IsPlaying);
                            var isPrefabInstance = targets.Any(PrefabUtility.IsPartOfPrefabInstance);
                            var isPrefabAsset = targets.Any(PrefabUtility.IsPartOfPrefabAsset);

                            var enabled = true;
                            var reason = default(string?);
                            switch (enableProperty)
                            {
                                case Availability.Always:
                                    break;
                                case Availability.RuntimeOnly:
                                    enabled = isPlaying;
                                    reason = "Unavailable in editor mode";
                                    break;
                                case Availability.Inherit:
                                    enabled = executeAlways || isPlaying;
                                    reason = "Unavailable in editor mode";
                                    break;
                                default:
                                    throw new ArgumentOutOfRangeException();
                            }

                            if (enabled)
                            {
                                switch (enablePropertyInPrefab)
                                {
                                    case PrefabAvailability.InstanceOnly:
                                        enabled = !isPrefabAsset;
                                        reason = "Unavailable for prefab asset";
                                        break;
                                    case PrefabAvailability.AssetOnly:
                                        enabled = !isPrefabInstance;
                                        reason = "Unavailable for prefab instance";
                                        break;
                                    case PrefabAvailability.Always:
                                        break;
                                    case PrefabAvailability.Never:
                                        enabled = !isPrefabAsset && !isPrefabInstance;
                                        reason = "Unavailable for prefab";
                                        break;
                                    default:
                                        throw new ArgumentOutOfRangeException();
                                }
                            }

                            if (enabled)
                            {
                                GUI.enabled = mutability == Writable.RuntimeOnly && displayedProperty.Property.CanWrite;
                                var changed = drawer.Draw(displayedProperty, targets);
                                if (mutability == Writable.RuntimeOnly && !isPlaying && changed)
                                {
                                    foreach (var t in targets) EditorUtility.SetDirty(t);
                                }

                                displayedProperty.IsOfUnsupportedType = false;
                            }
                            else
                            {
                                GUI.enabled = false;
                                var fieldName = ObjectNames.NicifyVariableName(displayedProperty.Property.Name);
                                if (reason is { } reasonValue)
                                {
                                    EditorGUILayout.LabelField(fieldName, reasonValue);
                                }
                                else
                                {
                                    EditorGUILayout.LabelField(fieldName);
                                }
                            }

                            displayedProperty.IsOfUnsupportedType = false;
                            break;
                        }
                    }

                    if (displayedProperty.IsOfUnsupportedType)
                    {
                        EditorGUILayout.HelpBox(
                            $"Unsupported property type {displayedProperty.Property.PropertyType.Name}.\n\n" +
                            $"You can add an Drawer<{displayedProperty.Property.PropertyType.Name}> to " +
                            $"{nameof(MonoBehaviourEditor)}.{nameof(drawers)} to add support for this type.",
                            MessageType.Error
                        );
                    }
                }
            }

            if (methodCalls.Count > 0)
            {
                foreach (var methodCall in methodCalls)
                {
                    if (methodCall.Arguments.Any()) GUILayout.BeginVertical("Box");

                    var isPlaying = targets.All(Application.IsPlaying);
                    GUI.enabled = methodCall.Availability switch
                    {
                        Availability.Always => true,
                        Availability.RuntimeOnly => isPlaying,
                        Availability.Inherit => executeAlways || isPlaying,
                        _ => throw new ArgumentOutOfRangeException()
                    };
                    if (methodCall.HasErrors) GUI.enabled = false;

                    if (GUILayout.Button(ObjectNames.NicifyVariableName(methodCall.Method.Name)))
                    {
                        // invoke for each target in selection
                        foreach (var target in targets)
                        {
                            var argumentValues = methodCall.Arguments.Select(a => a.Value).ToArray();
                            var result = methodCall.Method.Invoke(target, argumentValues);
                            if (result is IEnumerator enumerator && target is MonoBehaviour monoBehaviour)
                            {
                                monoBehaviour.StartCoroutine(enumerator);
                            }
                            else if (result != null)
                            {
                                Debug.Log("[EditorButton] " + methodCall.Method.Name + ": " + result);
                            }
                        }
                    }

                    foreach (var argument in methodCall.Arguments)
                    {
                        // If we encounter an argument we cannot serialize we show an error and disable the button
                        methodCall.HasErrors = true;
                        foreach (var argumentField in drawers)
                        {
                            if (argumentField.Supports(argument.Parameter.ParameterType))
                            {
                                argumentField.Draw(argument);
                                methodCall.HasErrors = false;
                                break;
                            }
                        }

                        if (methodCall.HasErrors)
                        {
                            EditorGUILayout.HelpBox(
                                $"Unsupported parameter type {argument.Parameter.ParameterType.Name}.\n\n" +
                                $"You can add an Drawer<{argument.Parameter.ParameterType.Name}> to " +
                                $"{nameof(MonoBehaviourEditor)}.{nameof(drawers)} to add support for this type.",
                                MessageType.Error
                            );
                        }
                    }

                    if (methodCall.Arguments.Any()) GUILayout.EndVertical();
                }
            }
        }

        private interface IDrawer
        {
            bool Supports(Type type);
            void Draw(Argument argument);

            /// <returns>true if changes were made</returns>
            bool Draw(DisplayedProperty displayedProperty, UnityEngine.Object[] targets);
        }

        /// <summary>
        /// Draws edit controls for a given type.
        ///
        /// For example a Drawer&lt;Vector3&gt; could draw an x, y, z int field.
        /// </summary>
        private class Drawer<T> : IDrawer
        {
            private readonly Func<string, T, T> drawMethod;

            public Drawer(Func<string, T, T> drawMethod)
            {
                this.drawMethod = drawMethod;
            }

            public bool Supports(Type type)
            {
                return typeof(T).IsAssignableFrom(type);
            }

            public void Draw(Argument argument)
            {
                if (!Supports(argument.Parameter.ParameterType))
                {
                    throw new InvalidOperationException(
                        "Unsupported argument type" + argument.Parameter.ParameterType + ". " +
                        "Use " + nameof(Supports) + " to check if argument is supported."
                    );
                }

                var fieldName = ObjectNames.NicifyVariableName(argument.Parameter.Name);
                argument.Value = drawMethod(fieldName, (T) argument.Value!);
            }

            public bool Draw(DisplayedProperty displayedProperty, UnityEngine.Object[] targets)
            {
                if (!Supports(displayedProperty.Property.PropertyType))
                {
                    throw new InvalidOperationException(
                        "Unsupported argument type" + displayedProperty.Property.PropertyType + ". " +
                        "Use " + nameof(Supports) + " to check if argument is supported."
                    );
                }


                // invoke for each target in selection
                var firstValue = default(object);
                var equal = true;
                for (var i = 0; i < targets.Length; i++)
                {
                    if (i == 0)
                    {
                        firstValue = displayedProperty.Property.GetMethod.Invoke(targets[i], null);
                    }
                    else
                    {
                        var value = displayedProperty.Property.GetMethod.Invoke(targets[i], null);
                        if (!Equals(value, firstValue)) equal = false;
                    }
                }

                var fieldName = ObjectNames.NicifyVariableName(displayedProperty.Property.Name);
                if (GUI.enabled)
                {
                    if (equal)
                    {
                        var newValue = drawMethod(fieldName, (T) firstValue!);

                        if (!Equals(newValue, firstValue))
                        {
                            foreach (var target in targets)
                            {
                                displayedProperty.Property.SetMethod.Invoke(target, new object[] {newValue!});
                            }

                            return true;
                        }
                    }
                    else
                    {
                        EditorGUI.showMixedValue = !equal;
                        var newValue = drawMethod(fieldName, default!);
                        EditorGUI.showMixedValue = false;

                        if (!Equals(newValue, default(T)))
                        {
                            foreach (var target in targets)
                            {
                                displayedProperty.Property.SetMethod.Invoke(target, new object[] {newValue!});
                            }

                            return true;
                        }
                    }
                }
                else
                {
                    EditorGUILayout.LabelField(fieldName, "Unavailable outside play");
                }

                return false;
            }
        }

        private class Argument
        {
            public object? Value { get; set; }
            public ParameterInfo Parameter { get; }

            public Argument(ParameterInfo parameter, object? value)
            {
                Parameter = parameter;
                Value = value;
            }
        }

        private class MethodCall
        {
            public MethodInfo Method { get; }
            public List<Argument> Arguments { get; }
            public Availability Availability { get; }
            public bool HasErrors { get; set; }

            public MethodCall(MethodInfo method, List<Argument> arguments, Availability availability)
            {
                Method = method;
                Arguments = arguments;
                Availability = availability;
            }
        }

        private class DisplayedProperty
        {
            public PropertyInfo Property { get; }
            public Writable Mutability { get; }
            public Availability Availability { get; }
            public PrefabAvailability PrefabAvailability { get; }
            public bool IsOfUnsupportedType { get; set; }

            public DisplayedProperty(
                PropertyInfo property,
                Writable mutability, Availability availability,
                PrefabAvailability prefabAvailability)
            {
                Property = property;
                Mutability = mutability;
                Availability = availability;
                PrefabAvailability = prefabAvailability;
            }
        }
    }
}