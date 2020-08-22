// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Reflection;

namespace Microsoft.AspNetCore.Components.Reflection
{
    internal sealed class PropertySetter
    {
        private static readonly MethodInfo CallPropertySetterOpenGenericMethod =
            typeof(PropertySetter).GetTypeInfo().GetDeclaredMethod(nameof(CallPropertySetter))!;

        private readonly Action<object, object> _setterDelegate;

        public PropertySetter(Type targetType, PropertyInfo property)
        {
            if (property.SetMethod == null)
            {
                throw new InvalidOperationException($"Cannot provide a value for property " +
                    $"'{property.Name}' on type '{targetType.FullName}' because the property " +
                    $"has no setter.");
            }

            var setMethod = property.SetMethod;

            var propertySetterAsAction =
                setMethod.CreateDelegate(typeof(Action<,>).MakeGenericType(targetType, property.PropertyType));
            var callPropertySetterClosedGenericMethod =
                CallPropertySetterOpenGenericMethod.MakeGenericMethod(targetType, property.PropertyType);
            _setterDelegate = (Action<object, object>)
                callPropertySetterClosedGenericMethod.CreateDelegate(typeof(Action<object, object>), propertySetterAsAction);
        }

        public int RequiredParameterId { get; set; }

        public bool Required { get; init; }

        public bool Cascading { get; init;  }

        public void SetValue(object target, object value) => _setterDelegate(target, value);

        private static void CallPropertySetter<TTarget, TValue>(
            Action<TTarget, TValue> setter,
            object target,
            object value)
            where TTarget : notnull
        {
            if (value == null)
            {
                setter((TTarget)target, default!);
            }
            else
            {
                setter((TTarget)target, (TValue)value);
            }
        }
    }
}
