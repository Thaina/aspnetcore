// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using Microsoft.AspNetCore.Components.Rendering;
using Xunit;

namespace Microsoft.AspNetCore.Components
{
    public partial class ParameterViewTest
    {
        [Fact]
        public void IncomingParameterMatchesAnnotatedPrivateProperty_SetsValue()
        {
            // Arrange
            var someObject = new object();
            var parameters = new ParameterViewBuilder
            {
                { nameof(HasInstanceProperties.IntProp), 123 },
                { nameof(HasInstanceProperties.StringProp), "Hello" },
                { HasInstanceProperties.ObjectPropName, someObject },
            }.Build();
            var target = new HasInstanceProperties();

            // Act
            parameters.SetParameterProperties(target);

            // Assert
            Assert.Equal(123, target.IntProp);
            Assert.Equal("Hello", target.StringProp);
            Assert.Same(someObject, target.ObjectPropCurrentValue);
        }

        [Fact]
        public void IncomingParameterMatchesDeclaredParameterCaseInsensitively_SetsValue()
        {
            // Arrange
            var parameters = new ParameterViewBuilder
            {
                { nameof(HasInstanceProperties.IntProp).ToLowerInvariant(), 123 }
            }.Build();
            var target = new HasInstanceProperties();

            // Act
            parameters.SetParameterProperties(target);

            // Assert
            Assert.Equal(123, target.IntProp);
        }

        [Fact]
        public void IncomingParameterMatchesInheritedDeclaredParameter_SetsValue()
        {
            // Arrange
            var parameters = new ParameterViewBuilder
            {
                { nameof(HasInheritedProperties.IntProp), 123 },
                { nameof(HasInheritedProperties.DerivedClassIntProp), 456 },
            }.Build();
            var target = new HasInheritedProperties();

            // Act
            parameters.SetParameterProperties(target);

            // Assert
            Assert.Equal(123, target.IntProp);
            Assert.Equal(456, target.DerivedClassIntProp);
        }

        [Fact]
        public void IncomingParameterMatchesOverriddenParameter_ThatDoesNotHaveAttribute()
        {
            // Test for https://github.com/dotnet/aspnetcore/issues/13162
            // Arrange
            var parameters = new ParameterViewBuilder
            {
                { nameof(DerivedType.VirtualProp), 123 },
            }.Build();
            var target = new DerivedType();

            // Act
            parameters.SetParameterProperties(target);

            // Assert
            Assert.Equal(123, target.VirtualProp);
        }

        [Fact]
        public void NoIncomingParameterMatchesDeclaredParameter_LeavesValueUnchanged()
        {
            // Arrange
            var existingObjectValue = new object();
            var target = new HasInstanceProperties
            {
                IntProp = 456,
                StringProp = "Existing value",
                ObjectPropCurrentValue = existingObjectValue
            };

            var parameters = new ParameterViewBuilder().Build();

            // Act
            parameters.SetParameterProperties(target);

            // Assert
            Assert.Equal(456, target.IntProp);
            Assert.Equal("Existing value", target.StringProp);
            Assert.Same(existingObjectValue, target.ObjectPropCurrentValue);
        }

        [Fact]
        public void IncomingCascadingValueMatchesCascadingParameter_SetsValue()
        {
            // Arrange
            var builder = new ParameterViewBuilder();
            builder.Add(nameof(HasCascadingParameter.Cascading), "hi", cascading: true);
            var parameters = builder.Build();

            var target = new HasCascadingParameter();

            // Act
            parameters.SetParameterProperties(target);

            // Assert
            Assert.Equal("hi", target.Cascading);
        }

        [Fact]
        public void NoIncomingCascadingValueMatchesDeclaredCascadingParameter_LeavesValueUnchanged()
        {
            // Arrange
            var builder = new ParameterViewBuilder();
            var parameters = builder.Build();

            var target = new HasCascadingParameter()
            {
                Cascading = "bye",
            };

            // Act
            parameters.SetParameterProperties(target);

            // Assert
            Assert.Equal("bye", target.Cascading);
        }

        [Fact]
        public void IncomingCascadingValueMatchesNoDeclaredParameter_Throws()
        {
            // Arrange
            var builder = new ParameterViewBuilder();
            builder.Add("SomethingElse", "hi", cascading: true);
            var parameters = builder.Build();

            var target = new HasCascadingParameter();

            // Act
            var ex = Assert.Throws<InvalidOperationException>(() => parameters.SetParameterProperties(target));

            // Assert
            Assert.Equal(
                $"Object of type '{typeof(HasCascadingParameter).FullName}' does not have a property " +
                $"matching the name 'SomethingElse'.",
                ex.Message);
        }

        [Fact]
        public void IncomingParameterMatchesPropertyNotDeclaredAsParameter_Throws()
        {
            // Arrange
            var target = new HasPropertyWithoutParameterAttribute();
            var parameters = new ParameterViewBuilder
            {
                { nameof(HasPropertyWithoutParameterAttribute.IntProp), 123 },
            }.Build();

            // Act
            var ex = Assert.Throws<InvalidOperationException>(
                () => parameters.SetParameterProperties(target));

            // Assert
            Assert.Equal(default, target.IntProp);
            Assert.Equal(
                $"Object of type '{typeof(HasPropertyWithoutParameterAttribute).FullName}' has a property matching the name '{nameof(HasPropertyWithoutParameterAttribute.IntProp)}', " +
                $"but it does not have [{nameof(ParameterAttribute)}] or [{nameof(CascadingParameterAttribute)}] applied.",
                ex.Message);
        }

        [Fact]
        public void IncomingParameterMatchesPropertyNotPublic_Throws()
        {
            // Arrange
            var target = new HasNonPublicPropertyWithParameterAttribute();
            var parameters = new ParameterViewBuilder
            {
                { nameof(HasNonPublicPropertyWithParameterAttribute.IntProp), 123 },
            }.Build();

            // Act
            var ex = Assert.Throws<InvalidOperationException>(
                () => parameters.SetParameterProperties(target));

            // Assert
            Assert.Equal(default, target.IntProp);
            Assert.Equal(
                $"The type '{typeof(HasNonPublicPropertyWithParameterAttribute).FullName}' declares a parameter matching the name '{nameof(HasNonPublicPropertyWithParameterAttribute.IntProp)}' that is not public. Parameters must be public.",
                ex.Message);
        }

        [Fact]
        public void IncomingCascadingParameterMatchesPropertyNotPublic_Works()
        {
            // Arrange
            var target = new HasNonPublicCascadingParameter();
            var builder = new ParameterViewBuilder();
            builder.Add("Cascading", "Test", cascading: true);
            var parameters = builder.Build();

            // Act
            parameters.SetParameterProperties(target);

            // Assert
            Assert.Equal("Test", target.GetCascadingValue());
        }

        [Fact]
        public void IncomingNonCascadingValueMatchesCascadingParameter_Throws()
        {
            // Arrange
            var target = new HasCascadingParameter();
            var parameters = new ParameterViewBuilder
            {
                { nameof(HasCascadingParameter.Cascading), 123 },
            }.Build();

            // Act
            var ex = Assert.Throws<InvalidOperationException>(() => parameters.SetParameterProperties(target));

            // Assert
            Assert.Equal(
                $"Object of type '{typeof(HasCascadingParameter).FullName}' has a property matching the name '{nameof(HasCascadingParameter.Cascading)}', " +
                $"but it does not have [{nameof(ParameterAttribute)}] applied.",
                ex.Message);
        }

        [Fact]
        public void IncomingCascadingValueMatchesNonCascadingParameter_Throws()
        {
            // Arrange
            var target = new HasInstanceProperties();
            var builder = new ParameterViewBuilder();
            builder.Add(nameof(HasInstanceProperties.IntProp), 16, cascading: true);
            var parameters = builder.Build();

            // Act
            var ex = Assert.Throws<InvalidOperationException>(() => parameters.SetParameterProperties(target));

            // Assert
            Assert.Equal(
                $"The property '{nameof(HasInstanceProperties.IntProp)}' on component type '{typeof(HasInstanceProperties).FullName}' " +
                $"cannot be set using a cascading value.",
                ex.Message);
        }

        [Fact]
        public void SettingCaptureUnmatchedValuesParameterExplicitlyWorks()
        {
            // Arrange
            var target = new HasCaptureUnmatchedValuesProperty();
            var value = new Dictionary<string, object>();
            var parameters = new ParameterViewBuilder
            {
                { nameof(HasCaptureUnmatchedValuesProperty.CaptureUnmatchedValues), value },
            }.Build();

            // Act
            parameters.SetParameterProperties(target);

            // Assert
            Assert.Same(value, target.CaptureUnmatchedValues);
        }

        [Fact]
        public void SettingCaptureUnmatchedValuesParameterWithUnmatchedValuesWorks()
        {
            // Arrange
            var target = new HasCaptureUnmatchedValuesProperty();
            var parameters = new ParameterViewBuilder
            {
                { nameof(HasCaptureUnmatchedValuesProperty.StringProp), "hi" },
                { "test1", 123 },
                { "test2", 456 },
            }.Build();

            // Act
            parameters.SetParameterProperties(target);

            // Assert
            Assert.Equal("hi", target.StringProp);
            Assert.Collection(
                target.CaptureUnmatchedValues.OrderBy(kvp => kvp.Key),
                kvp =>
                {
                    Assert.Equal("test1", kvp.Key);
                    Assert.Equal(123, kvp.Value);
                },
                kvp =>
                {
                    Assert.Equal("test2", kvp.Key);
                    Assert.Equal(456, kvp.Value);
                });
        }

        [Fact]
        public void SettingCaptureUnmatchedValuesParameterExplicitlyAndImplicitly_Throws()
        {
            // Arrange
            var target = new HasCaptureUnmatchedValuesProperty();
            var parameters = new ParameterViewBuilder
            {
                { nameof(HasCaptureUnmatchedValuesProperty.CaptureUnmatchedValues), new Dictionary<string, object>() },
                { "test1", 123 },
                { "test2", 456 },
            }.Build();

            // Act
            var ex = Assert.Throws<InvalidOperationException>(() => parameters.SetParameterProperties(target));

            // Assert
            Assert.Equal(
                $"The property '{nameof(HasCaptureUnmatchedValuesProperty.CaptureUnmatchedValues)}' on component type '{typeof(HasCaptureUnmatchedValuesProperty).FullName}' cannot be set explicitly when " +
                $"also used to capture unmatched values. Unmatched values:" + Environment.NewLine +
                $"test1" + Environment.NewLine +
                $"test2",
                ex.Message);
        }

        [Fact]
        public void SettingCaptureUnmatchedValuesParameterExplicitlyAndImplicitly_ReverseOrder_Throws()
        {
            // Arrange
            var target = new HasCaptureUnmatchedValuesProperty();
            var parameters = new ParameterViewBuilder
            {
                { "test2", 456 },
                { "test1", 123 },
                { nameof(HasCaptureUnmatchedValuesProperty.CaptureUnmatchedValues), new Dictionary<string, object>() },
            }.Build();

            // Act
            var ex = Assert.Throws<InvalidOperationException>(() => parameters.SetParameterProperties(target));

            // Assert
            Assert.Equal(
                $"The property '{nameof(HasCaptureUnmatchedValuesProperty.CaptureUnmatchedValues)}' on component type '{typeof(HasCaptureUnmatchedValuesProperty).FullName}' cannot be set explicitly when " +
                $"also used to capture unmatched values. Unmatched values:" + Environment.NewLine +
                $"test1" + Environment.NewLine +
                $"test2",
                ex.Message);
        }

        [Fact]
        public void HasDuplicateCaptureUnmatchedValuesParameters_Throws()
        {
            // Arrange
            var target = new HasDuplicateCaptureUnmatchedValuesProperty();
            var parameters = new ParameterViewBuilder().Build();

            // Act
            var ex = Assert.Throws<InvalidOperationException>(() => parameters.SetParameterProperties(target));

            // Assert
            Assert.Equal(
                $"Multiple properties were found on component type '{typeof(HasDuplicateCaptureUnmatchedValuesProperty).FullName}' " +
                $"with '{nameof(ParameterAttribute)}.{nameof(ParameterAttribute.CaptureUnmatchedValues)}'. " +
                $"Only a single property per type can use '{nameof(ParameterAttribute)}.{nameof(ParameterAttribute.CaptureUnmatchedValues)}'. " +
                $"Properties:" + Environment.NewLine +
                $"{nameof(HasDuplicateCaptureUnmatchedValuesProperty.CaptureUnmatchedValuesProp1)}" + Environment.NewLine +
                $"{nameof(HasDuplicateCaptureUnmatchedValuesProperty.CaptureUnmatchedValuesProp2)}",
                ex.Message);
        }

        [Fact]
        public void HasCapturedUnmatchedValuesParametersWithRequired_Throws()
        {
            // Arrange
            var target = new HasRequiredCapturedUnmatchedValuesProperty();
            var parameters = new ParameterViewBuilder().Build();
            var expected = $"Parameter {nameof(HasRequiredCapturedUnmatchedValuesProperty.CaptureUnmatchedValuesProp)} on component type '{target.GetType().FullName}' cannot have both " +
                $"'{nameof(ParameterAttribute.CaptureUnmatchedValues)}' and {nameof(ParameterAttribute.Required)} set.";

            // Act
            var ex = Assert.Throws<InvalidOperationException>(() => parameters.SetParameterProperties(target));

            // Assert
            Assert.Equal(expected, ex.Message);
        }

        [Fact]
        public void HasCaptureUnmatchedValuesParameterWithWrongType_Throws()
        {
            // Arrange
            var target = new HasWrongTypeCaptureUnmatchedValuesProperty();
            var parameters = new ParameterViewBuilder().Build();

            // Act
            var ex = Assert.Throws<InvalidOperationException>(() => parameters.SetParameterProperties(target));

            // Assert
            Assert.Equal(
                $"The property '{nameof(HasWrongTypeCaptureUnmatchedValuesProperty.CaptureUnmatchedValuesProp)}' on component type '{typeof(HasWrongTypeCaptureUnmatchedValuesProperty).FullName}' cannot be used with " +
                $"'{nameof(ParameterAttribute)}.{nameof(ParameterAttribute.CaptureUnmatchedValues)}' because it has the wrong type. " +
                $"The property must be assignable from 'Dictionary<string, object>'.",
                ex.Message);
        }

        [Fact]
        public void IncomingNonCascadingValueMatchesCascadingParameter_WithCaptureUnmatchedValues_DoesNotThrow()
        {
            // Arrange
            var target = new HasCaptureUnmatchedValuesPropertyAndCascadingParameter()
            {
                Cascading = "bye",
            };
            var parameters = new ParameterViewBuilder
            {
                { nameof(HasCaptureUnmatchedValuesPropertyAndCascadingParameter.Cascading), "hi" },
            }.Build();

            // Act
            parameters.SetParameterProperties(target);

            Assert.Collection(
                target.CaptureUnmatchedValues,
                kvp =>
                {
                    Assert.Equal(nameof(HasCaptureUnmatchedValuesPropertyAndCascadingParameter.Cascading), kvp.Key);
                    Assert.Equal("hi", kvp.Value);
                });
            Assert.Equal("bye", target.Cascading);
        }

        [Fact]
        public void IncomingCascadingValueMatchesNonCascadingParameter_WithCaptureUnmatchedValues_Throws()
        {
            // Arrange
            var target = new HasCaptureUnmatchedValuesProperty();
            var builder = new ParameterViewBuilder();
            builder.Add(nameof(HasInstanceProperties.IntProp), 16, cascading: true);
            var parameters = builder.Build();

            // Act
            var ex = Assert.Throws<InvalidOperationException>(() => parameters.SetParameterProperties(target));

            // Assert
            Assert.Equal(
                $"The property '{nameof(HasCaptureUnmatchedValuesProperty.IntProp)}' on component type '{typeof(HasCaptureUnmatchedValuesProperty).FullName}' " +
                $"cannot be set using a cascading value.",
                ex.Message);
        }

        [Fact]
        public void IncomingParameterValueMismatchesDeclaredParameterType_Throws()
        {
            // Arrange
            var someObject = new object();
            var parameters = new ParameterViewBuilder
            {
                { nameof(HasInstanceProperties.IntProp), "string value" },
            }.Build();
            var target = new HasInstanceProperties();

            // Act
            var ex = Assert.Throws<InvalidOperationException>(
                () => parameters.SetParameterProperties(target));

            // Assert
            Assert.Equal(
                $"Unable to set property '{nameof(HasInstanceProperties.IntProp)}' on object of " +
                $"type '{typeof(HasInstanceProperties).FullName}'. The error was: {ex.InnerException.Message}",
                ex.Message);
        }

        [Fact]
        public void PropertyExplicitSetterException_Throws()
        {
            // Arrange
            var target = new HasPropertyWhoseSetterThrows();
            var parameters = new ParameterViewBuilder
            {
                { nameof(HasPropertyWhoseSetterThrows.StringProp), "anything" },
            }.Build();

            // Act
            var ex = Assert.Throws<InvalidOperationException>(
                () => parameters.SetParameterProperties(target));

            // Assert
            Assert.Equal(
                $"Unable to set property '{nameof(HasPropertyWhoseSetterThrows.StringProp)}' on object of " +
                $"type '{typeof(HasPropertyWhoseSetterThrows).FullName}'. The error was: {ex.InnerException.Message}",
                ex.Message);
        }

        [Fact]
        public void DeclaredParametersVaryOnlyByCase_Throws()
        {
            // Arrange
            var parameters = new ParameterViewBuilder().Build();
            var target = new HasParametersVaryingOnlyByCase();

            // Act
            var ex = Assert.Throws<InvalidOperationException>(() =>
                parameters.SetParameterProperties(target));

            // Assert
            Assert.Equal(
                $"The type '{typeof(HasParametersVaryingOnlyByCase).FullName}' declares more than one parameter matching the " +
                $"name '{nameof(HasParametersVaryingOnlyByCase.MyValue).ToLowerInvariant()}'. Parameter names are case-insensitive and must be unique.",
                ex.Message);
        }

        [Fact]
        public void DeclaredParameterClashesWithInheritedParameter_Throws()
        {
            // Even when the developer uses 'new' to shadow an inherited property, this is not
            // an allowed scenario because there would be no way for the consumer to specify
            // both property values, and it's no good leaving the shadowed one unset because the
            // base class can legitimately depend on it for correct functioning.

            // Arrange
            var parameters = new ParameterViewBuilder().Build();
            var target = new HasParameterClashingWithInherited();

            // Act
            var ex = Assert.Throws<InvalidOperationException>(() =>
                parameters.SetParameterProperties(target));

            // Assert
            Assert.Equal(
                $"The type '{typeof(HasParameterClashingWithInherited).FullName}' declares more than one parameter matching the " +
                $"name '{nameof(HasParameterClashingWithInherited.IntProp).ToLowerInvariant()}'. Parameter names are case-insensitive and must be unique.",
                ex.Message);
        }

        [Fact]
        public void RequiredParameter_NoneSpecified_Throws()
        {
            // Arrange
            var parameters = new ParameterViewBuilder().Build();
            var target = new HasRequiredParameters();
            var expected = $"Component '{target.GetType().FullName}' requires a value for the parameter '{nameof(HasRequiredParameters.IntProperty)}'.";

            // Act
            var ex = Assert.Throws<InvalidOperationException>(() =>
                parameters.SetParameterProperties(target));

            // Assert
            Assert.Equal(expected, ex.Message);
        }

        [Fact]
        public void RequiredParameter_SomeSpecified_Throws()
        {
            // Arrange
            var parameters = new ParameterViewBuilder
            {
                { nameof(HasRequiredParameters.StringProperty), "some-value" },
            }.Build();
            var target = new HasRequiredParameters();
            var expected = $"Component '{target.GetType().FullName}' requires a value for the parameter '{nameof(HasRequiredParameters.IntProperty)}'.";

            // Act
            var ex = Assert.Throws<InvalidOperationException>(() =>
                parameters.SetParameterProperties(target));

            // Assert
            Assert.Equal(expected, ex.Message);
        }

        [Fact]
        public void RequiredParameter_AllSpecified_DoesNotThrow()
        {
            // Arrange
            var parameters = new ParameterViewBuilder
            {
                { nameof(HasRequiredParameters.StringProperty), "some-value" },
                { nameof(HasRequiredParameters.IntProperty), 7 },
            }.Build();
            var target = new HasRequiredParameters();

            // Act
            parameters.SetParameterProperties(target);

            // Assert
            Assert.Equal("some-value", target.StringProperty);
            Assert.Equal(7, target.IntProperty);
        }

        [Fact]
        public void RequiredParameter_ParameterPresentMultipleTimesOtherMissing_Throws()
        {
            // Arrange
            // In this case, the required parameter StringProperty is specified multiple times, but required
            // parameter IntProperty is never specified.
            var parameters = new ParameterViewBuilder
            {
                { nameof(HasRequiredParameters.StringProperty), "some-value" },
                { nameof(HasRequiredParameters.StringProperty), "different-value" },
            }.Build();
            var target = new HasRequiredParameters();
            var expected = $"Component '{target.GetType().FullName}' requires a value for the parameter '{nameof(SomeRequiredParameters.IntProperty)}'.";

            // Act
            var ex = Assert.Throws<InvalidOperationException>(() =>
                parameters.SetParameterProperties(target));

            // Assert
            Assert.Equal(expected, ex.Message);
        }

        [Fact]
        public void RequiredParameter_AllRequiredParametersPresent_Works()
        {
            // Arrange
            // In this case, the required parameter StringProperty is specified multiple times, but required
            // parameter IntProperty is never specified.
            var parameters = new ParameterViewBuilder
            {
                { nameof(HasRequiredParameters.StringProperty), "some-value" },
                { nameof(HasRequiredParameters.IntProperty), 8 },
                { nameof(HasRequiredParameters.IntProperty), 9 },
                { nameof(HasRequiredParameters.StringProperty), "different-value" },
            }.Build();
            var target = new HasRequiredParameters();

            // Act
            parameters.SetParameterProperties(target);

            // Assert
            Assert.Equal("different-value", target.StringProperty);
            Assert.Equal(9, target.IntProperty);
        }

        [Fact]
        public void RequiredParameter_SpecifyingNullValueWorks()
        {
            // Arrange
            var parameters = new ParameterViewBuilder
            {
                { nameof(HasRequiredParameters.StringProperty), null },
                { nameof(HasRequiredParameters.IntProperty), 7 },
            }.Build();
            var target = new HasRequiredParameters();

            // Act
            parameters.SetParameterProperties(target);

            // Assert
            Assert.Null(target.StringProperty);
            Assert.Equal(7, target.IntProperty);
        }

        [Fact]
        public void SomeRequiredParameter_NoneSpecified_Throws()
        {
            // Arrange
            var parameters = new ParameterViewBuilder
            {
            }.Build();
            var target = new SomeRequiredParameters();
            var expected = $"Component '{target.GetType().FullName}' requires a value for the parameter '{nameof(SomeRequiredParameters.StringProperty)}'.";

            // Act
            var ex = Assert.Throws<InvalidOperationException>(() =>
                parameters.SetParameterProperties(target));

            // Assert
            Assert.Equal(expected, ex.Message);
        }

        [Fact]
        public void SomeRequiredParameter_RequiredParameterNotSpecified_Throws()
        {
            // Arrange
            var parameters = new ParameterViewBuilder
            {
                { nameof(SomeRequiredParameters.IntProperty), 8 },
            }.Build();
            var target = new SomeRequiredParameters();
            var expected = $"Component '{target.GetType().FullName}' requires a value for the parameter '{nameof(SomeRequiredParameters.StringProperty)}'.";

            // Act
            var ex = Assert.Throws<InvalidOperationException>(() =>
                parameters.SetParameterProperties(target));

            // Assert
            Assert.Equal(expected, ex.Message);
        }

        [Fact]
        public void SomeRequiredParameter_RequiredParameterSpecified_DoesNotThrow()
        {
            // Arrange
            var parameters = new ParameterViewBuilder
            {
                { nameof(SomeRequiredParameters.StringProperty), "some-value" },
            }.Build();
            var target = new SomeRequiredParameters();

            // Act
            parameters.SetParameterProperties(target);

            // Assert
            Assert.Equal("some-value", target.StringProperty);
        }

        [Fact]
        public void RequiredCascadingParameter_ValueNotSpecified_Throws()
        {
            // Arrange
            var parameters = new ParameterViewBuilder
            {
                { nameof(RequiredCascadingParameter.IntProperty), 3 },
            }.Build();
            var target = new RequiredCascadingParameter();
            var expected = $"Component '{target.GetType().FullName}' requires a value for the cascading parameter '{nameof(RequiredCascadingParameter.StringProperty)}'.";

            // Act
            var ex = Assert.Throws<InvalidOperationException>(() =>
                parameters.SetParameterProperties(target));

            // Assert
            Assert.Equal(expected, ex.Message);
        }

        [Fact]
        public void RequiredCascadingParameter_ValueSpecified_DoesNotThrow()
        {
            // Arrange
            var builder = new ParameterViewBuilder();
            builder.Add(nameof(RequiredCascadingParameter.StringProperty), "some-value", cascading: true);
            var parameters = builder.Build();
            var target = new RequiredCascadingParameter();

            // Act
            parameters.SetParameterProperties(target);

            // Assert
            Assert.Equal("some-value", target.StringProperty);
        }

        [Theory]
        [InlineData(14)]
        [InlineData(31)]
        [InlineData(32)]
        public void RequireParameter_WorksForTypeWithFewerThan33Parameters(int numRequiredParameters)
        {
            // Arrange
            var builder = new ParameterViewBuilder();
            for (var i = 0; i < numRequiredParameters; i++)
            {
                builder.Add($"Property{i}", "value", false);
            }
            var parameters = builder.Build();
            var target = GenerateTypeWithNProperties(n: numRequiredParameters);

            // Act
            parameters.SetParameterProperties(target);

            // If we go this far, it's good
        }

        [Theory]
        [InlineData(33)]
        [InlineData(59)]
        public void RequiredParameter_ThrowsIfTypeHasMoreThan32Parameters(int numRequiredParameters)
        {
            // Arrange
            var builder = new ParameterViewBuilder();
            for (var i = 0; i < numRequiredParameters; i++)
            {
                builder.Add($"Property{i}", "value", false);
            }
            var parameters = builder.Build();
            var target = GenerateTypeWithNProperties(n: numRequiredParameters);
            var expected = $"The component '{target.GetType().FullName}' declares more than 32 'required' parameters. A component may have at most 32 required parameters.";

            // Act & Assert
            var ex = Assert.Throws<NotSupportedException>(() => parameters.SetParameterProperties(target));
            Assert.Equal(expected, ex.Message);
        }

        private static object GenerateTypeWithNProperties(int n)
        {
            var assemblyBuilder = AssemblyBuilder.DefineDynamicAssembly(new AssemblyName("Test"), AssemblyBuilderAccess.RunAndCollect);
            var module = assemblyBuilder.DefineDynamicModule("Default");
            var typeBuilder = module.DefineType("MyComponent");
            var parameterAttribute = typeof(ParameterAttribute).GetConstructor(Type.EmptyTypes);
            var requiredProperty = typeof(ParameterAttribute).GetProperty(nameof(ParameterAttribute.Required));

            for (var i = 0; i < n; i++)
            {
                var property = typeBuilder.DefineProperty($"Property{i}", PropertyAttributes.None, typeof(string), null);
                property.SetCustomAttribute(new CustomAttributeBuilder(parameterAttribute, new object[0], namedProperties: new[] { requiredProperty }, new object[] { true }));
                var attributes = MethodAttributes.Public | MethodAttributes.SpecialName | MethodAttributes.HideBySig;
                var getMethod = typeBuilder.DefineMethod($"get-Property{i}", attributes, typeof(string), Type.EmptyTypes);
                getMethod.GetILGenerator().ThrowException(typeof(Exception));
                property.SetGetMethod(getMethod);

                var setMethod = typeBuilder.DefineMethod($"set_Property{i}", attributes, typeof(void), new[] { typeof(string) });
                setMethod.GetILGenerator().Emit(OpCodes.Ret);
                property.SetSetMethod(setMethod);
            }
            var type = typeBuilder.CreateType();
            var target = Activator.CreateInstance(type);
            return target;
        }

        [Fact]
        public void SupplyingNullWritesDefaultForType()
        {
            // Arrange
            var parameters = new ParameterViewBuilder
            {
                { nameof(HasInstanceProperties.IntProp), null },
                { nameof(HasInstanceProperties.StringProp), null },
            }.Build();
            var target = new HasInstanceProperties { IntProp = 123, StringProp = "Hello" };

            // Act
            parameters.SetParameterProperties(target);

            // Assert
            Assert.Equal(0, target.IntProp);
            Assert.Null(target.StringProp);
        }

        class HasInstanceProperties
        {
            [Parameter] public int IntProp { get; set; }
            [Parameter] public string StringProp { get; set; }

            [Parameter] public object ObjectProp { get; set; }

            public static string ObjectPropName => nameof(ObjectProp);
            public object ObjectPropCurrentValue
            {
                get => ObjectProp;
                set => ObjectProp = value;
            }
        }

        class HasCascadingParameter
        {
            [CascadingParameter] public string Cascading { get; set; }
        }

        class HasPropertyWithoutParameterAttribute
        {
            public int IntProp { get; set; }
        }

        class HasNonPublicPropertyWithParameterAttribute
        {
            [Parameter]
            internal int IntProp { get; set; }
        }

        class HasPropertyWhoseSetterThrows
        {
            [Parameter]
            public string StringProp
            {
                get => string.Empty;
                set => throw new InvalidOperationException("This setter throws");
            }
        }

        class HasInheritedProperties : HasInstanceProperties
        {
            [Parameter] public int DerivedClassIntProp { get; set; }
        }

        class BaseType
        {
            [Parameter] public virtual int VirtualProp { get; set; }
        }

        class DerivedType : BaseType
        {
            public override int VirtualProp { get; set; }
        }

        class HasParametersVaryingOnlyByCase
        {
            [Parameter] public object MyValue { get; set; }
            [Parameter] public object Myvalue { get; set; }
        }

        class HasParameterClashingWithInherited : HasInstanceProperties
        {
            [Parameter] public new int IntProp { get; set; }
        }

        class HasCaptureUnmatchedValuesProperty
        {
            [Parameter] public int IntProp { get; set; }
            [Parameter] public string StringProp { get; set; }
            [Parameter] public object ObjectProp { get; set; }
            [Parameter(CaptureUnmatchedValues = true)] public IReadOnlyDictionary<string, object> CaptureUnmatchedValues { get; set; }
        }

        class HasCaptureUnmatchedValuesPropertyAndCascadingParameter
        {
            [CascadingParameter] public string Cascading { get; set; }
            [Parameter(CaptureUnmatchedValues = true)] public IReadOnlyDictionary<string, object> CaptureUnmatchedValues { get; set; }
        }

        class HasDuplicateCaptureUnmatchedValuesProperty
        {
            [Parameter(CaptureUnmatchedValues = true)] public Dictionary<string, object> CaptureUnmatchedValuesProp1 { get; set; }
            [Parameter(CaptureUnmatchedValues = true)] public IDictionary<string, object> CaptureUnmatchedValuesProp2 { get; set; }
        }

        class HasWrongTypeCaptureUnmatchedValuesProperty
        {
            [Parameter(CaptureUnmatchedValues = true)] public KeyValuePair<string, object>[] CaptureUnmatchedValuesProp { get; set; }
        }

        class HasRequiredCapturedUnmatchedValuesProperty
        {
            [Parameter(CaptureUnmatchedValues = true, Required = true)] public IDictionary<string, object> CaptureUnmatchedValuesProp { get; set; }
        }

        class HasNonPublicCascadingParameter
        {
            [CascadingParameter] private string Cascading { get; set; }

            public string GetCascadingValue() => Cascading;
        }

        class HasRequiredParameters
        {
            [Parameter(Required = true)]
            public string StringProperty { get; set; }

            [Parameter(Required = true)]
            public int IntProperty { get; set; }
        }

        class SomeRequiredParameters
        {
            [Parameter(Required = true)]
            public string StringProperty { get; set; }

            [Parameter]
            public int IntProperty { get; set; }
        }

        class RequiredCascadingParameter
        {
            [CascadingParameter(Required = true)]
            public string StringProperty { get; set; }

            [Parameter]
            public int IntProperty { get; set; }
        }

        class ParameterViewBuilder : IEnumerable
        {
            private readonly List<(string Name, object Value, bool Cascading)> _keyValuePairs
                = new List<(string, object, bool)>();

            public void Add(string name, object value, bool cascading = false)
                => _keyValuePairs.Add((name, value, cascading));

            public IEnumerator GetEnumerator()
                => throw new NotImplementedException();

            public ParameterView Build()
            {
                var builder = new RenderTreeBuilder();

                builder.OpenComponent<FakeComponent>(0);
                foreach (var kvp in _keyValuePairs)
                {
                    if (!kvp.Cascading)
                    {
                        builder.AddAttribute(1, kvp.Name, kvp.Value);
                    }
                }
                builder.CloseComponent();

                var view = new ParameterView(ParameterViewLifetime.Unbound, builder.GetFrames().Array, ownerIndex: 0);

                var cascadingParameters = new List<CascadingParameterState>();
                foreach (var kvp in _keyValuePairs)
                {
                    if (kvp.Cascading)
                    {
                        cascadingParameters.Add(new CascadingParameterState(kvp.Name, new TestCascadingValueProvider(kvp.Value)));
                    }
                }

                return view.WithCascadingParameters(cascadingParameters);
            }
        }

        private class TestCascadingValueProvider : ICascadingValueComponent
        {
            public TestCascadingValueProvider(object value)
            {
                CurrentValue = value;
            }

            public object CurrentValue { get; }

            public bool CurrentValueIsFixed => throw new NotImplementedException();

            public bool CanSupplyValue(Type valueType, string valueName)
            {
                throw new NotImplementedException();
            }

            public void Subscribe(ComponentState subscriber)
            {
                throw new NotImplementedException();
            }

            public void Unsubscribe(ComponentState subscriber)
            {
                throw new NotImplementedException();
            }
        }
    }
}
