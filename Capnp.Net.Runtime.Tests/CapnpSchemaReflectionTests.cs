using System;
using System.Collections.Generic;
using Capnp;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Capnp.Net.Runtime.Tests
{
    [TestClass]
    [TestCategory("Coverage")]
    public class CapnpSchemaReflectionTests
    {
        // Test classes for reflection analysis
        [TypeId(0x1234567890ABCDEFUL)]
        public class TestStruct : ICapnpSerializable
        {
            public string Name { get; set; }
            public int Age { get; set; }
            public bool IsActive { get; set; }
            public List<string> Tags { get; set; }
            public TestEnum Status { get; set; }

            public void Serialize(SerializerState state)
            {
                throw new NotImplementedException();
            }

            public void Deserialize(DeserializerState state)
            {
                throw new NotImplementedException();
            }
        }

        [TypeId(0xFEDCBA0987654321UL)]
        public enum TestEnum : ushort
        {
            None = 0,
            Active = 1,
            Inactive = 2,
            Pending = 3
        }

        public class TestStructWithoutTypeId : ICapnpSerializable
        {
            public string Name { get; set; }
            public int Value { get; set; }

            public void Serialize(SerializerState state)
            {
                throw new NotImplementedException();
            }

            public void Deserialize(DeserializerState state)
            {
                throw new NotImplementedException();
            }
        }

        [TypeId(0x1111111111111111UL)]
        public interface ITestInterface
        {
            void Method1();
            string Method2(int param);
        }

        [TestMethod]
        public void GenerateSchema_WithValidStruct_ReturnsCorrectDefinition()
        {
            // Act
            var definition = CapnpSchemaReflection.GenerateSchema(typeof(TestStruct));

            // Assert
            Assert.IsNotNull(definition);
            Assert.AreEqual("TestStruct", definition.Name);
            Assert.AreEqual(0x1234567890ABCDEFUL, definition.TypeId);
            Assert.AreEqual(CapnpSchemaReflection.TypeKind.Struct, definition.Kind);
            Assert.IsTrue(definition.Fields.Count > 0);
        }

        [TestMethod]
        public void GenerateSchema_WithEnum_ReturnsCorrectDefinition()
        {
            // Act
            var definition = CapnpSchemaReflection.GenerateSchema(typeof(TestEnum));

            // Assert
            Assert.IsNotNull(definition);
            Assert.AreEqual("TestEnum", definition.Name);
            Assert.AreEqual(0xFEDCBA0987654321UL, definition.TypeId);
            Assert.AreEqual(CapnpSchemaReflection.TypeKind.Enum, definition.Kind);
            Assert.AreEqual(4, definition.EnumValues.Count);

            // Check enum values
            Assert.AreEqual("None", definition.EnumValues[0].Name);
            Assert.AreEqual(0, definition.EnumValues[0].Value);
            Assert.AreEqual("Active", definition.EnumValues[1].Name);
            Assert.AreEqual(1, definition.EnumValues[1].Value);
        }

        [TestMethod]
        public void GenerateSchema_WithInterface_ReturnsCorrectDefinition()
        {
            // Act
            var definition = CapnpSchemaReflection.GenerateSchema(typeof(ITestInterface));

            // Assert
            Assert.IsNotNull(definition);
            Assert.AreEqual("ITestInterface", definition.Name);
            Assert.AreEqual(0x1111111111111111UL, definition.TypeId);
            Assert.AreEqual(CapnpSchemaReflection.TypeKind.Interface, definition.Kind);
            Assert.IsTrue(definition.Fields.Count > 0);
        }

        [TestMethod]
        public void GenerateSchema_WithoutTypeIdAttribute_GeneratesTypeId()
        {
            // Act
            var definition = CapnpSchemaReflection.GenerateSchema(typeof(TestStructWithoutTypeId));

            // Assert
            Assert.IsNotNull(definition);
            Assert.AreEqual("TestStructWithoutTypeId", definition.Name);
            Assert.AreNotEqual(0UL, definition.TypeId); // Should generate a non-zero TypeId
            Assert.AreEqual(CapnpSchemaReflection.TypeKind.Struct, definition.Kind);
        }

        [TestMethod]
        public void GenerateSchema_WithNullType_ThrowsArgumentNullException()
        {
            // Act & Assert
            Assert.ThrowsException<ArgumentNullException>(() =>
                CapnpSchemaReflection.GenerateSchema(null));
        }

        [TestMethod]
        public void GenerateSchema_WithUnsupportedType_ThrowsArgumentException()
        {
            // Act & Assert
            Assert.ThrowsException<ArgumentException>(() =>
                CapnpSchemaReflection.GenerateSchema(typeof(string)));
        }

        [TestMethod]
        public void GenerateSchemaText_WithStruct_ReturnsValidCapnpSchema()
        {
            // Act
            var schemaText = CapnpSchemaReflection.GenerateSchemaText(typeof(TestStruct));

            // Assert
            Assert.IsNotNull(schemaText);
            Assert.IsTrue(schemaText.Contains("struct TestStruct"));
            Assert.IsTrue(schemaText.Contains("@0x1234567890ABCDEF"));
            Assert.IsTrue(schemaText.Contains("name"));
            Assert.IsTrue(schemaText.Contains("age"));
            Assert.IsTrue(schemaText.Contains("isActive"));
        }

        [TestMethod]
        public void GenerateSchemaText_WithEnum_ReturnsValidCapnpSchema()
        {
            // Act
            var schemaText = CapnpSchemaReflection.GenerateSchemaText(typeof(TestEnum));

            // Assert
            Assert.IsNotNull(schemaText);
            Assert.IsTrue(schemaText.Contains("enum TestEnum"));
            Assert.IsTrue(schemaText.Contains("@0xFEDCBA0987654321"));
            Assert.IsTrue(schemaText.Contains("none @0"));
            Assert.IsTrue(schemaText.Contains("active @1"));
            Assert.IsTrue(schemaText.Contains("inactive @2"));
            Assert.IsTrue(schemaText.Contains("pending @3"));
        }

        [TestMethod]
        public void GenerateSchemaText_WithInterface_ReturnsValidCapnpSchema()
        {
            // Act
            var schemaText = CapnpSchemaReflection.GenerateSchemaText(typeof(ITestInterface));

            // Assert
            Assert.IsNotNull(schemaText);
            Assert.IsTrue(schemaText.Contains("interface ITestInterface"));
            Assert.IsTrue(schemaText.Contains("@0x1111111111111111"));
            Assert.IsTrue(schemaText.Contains("method1()"));
            Assert.IsTrue(schemaText.Contains("method2()"));
        }

        [TestMethod]
        public void MapCSharpTypeToCapnp_PrimitiveTypes_ReturnsCorrectCapnpTypes()
        {
            // Test primitive type mapping through the public API
            var definition = CapnpSchemaReflection.GenerateSchema(typeof(TestStruct));

            // Verify that primitive types are mapped correctly
            var nameField = definition.Fields.Find(f => f.Name == "Name");
            Assert.IsNotNull(nameField);
            Assert.AreEqual(CapnpSchemaReflection.TypeKind.Text, nameField.Type.Kind);

            var ageField = definition.Fields.Find(f => f.Name == "Age");
            Assert.IsNotNull(ageField);
            Assert.AreEqual(CapnpSchemaReflection.TypeKind.Primitive, ageField.Type.Kind);
            Assert.AreEqual("int32", ageField.Type.TypeName?.ToLowerInvariant());
        }

        [TestMethod]
        public void GenerateSchema_WithNestedTypes_HandlesCorrectly()
        {
            // This test would require a more complex test class with nested types
            // For now, we verify the basic functionality works
            var definition = CapnpSchemaReflection.GenerateSchema(typeof(TestStruct));
            Assert.IsNotNull(definition.NestedTypes);
        }

        [TestMethod]
        public void GenerateSchema_WithListTypes_HandlesCorrectly()
        {
            // Act
            var definition = CapnpSchemaReflection.GenerateSchema(typeof(TestStruct));

            // Assert
            Assert.IsNotNull(definition);
            var tagsField = definition.Fields.Find(f => f.Name == "Tags");
            Assert.IsNotNull(tagsField);
            Assert.AreEqual(CapnpSchemaReflection.TypeKind.List, tagsField.Type.Kind);
            Assert.IsNotNull(tagsField.Type.ElementType);
        }

        [TestMethod]
        public void GenerateSchema_WithEnumField_HandlesCorrectly()
        {
            // Act
            var definition = CapnpSchemaReflection.GenerateSchema(typeof(TestStruct));

            // Assert
            Assert.IsNotNull(definition);
            var statusField = definition.Fields.Find(f => f.Name == "Status");
            Assert.IsNotNull(statusField);
            Assert.AreEqual(CapnpSchemaReflection.TypeKind.Enum, statusField.Type.Kind);
            Assert.AreEqual("TestEnum", statusField.Type.TypeName);
        }

        [TestMethod]
        public void GenerateSchema_WithDateTimeFields_HandlesCorrectly()
        {
            // Arrange
            var testClass = typeof(DateTimeTestClass);

            // Act
            var definition = CapnpSchemaReflection.GenerateSchema(testClass);

            // Assert
            Assert.IsNotNull(definition);

            var dateTimeField = definition.Fields.Find(f => f.Name == "DateTimeField");
            Assert.IsNotNull(dateTimeField);
            Assert.AreEqual(CapnpSchemaReflection.TypeKind.Primitive, dateTimeField.Type.Kind);
            Assert.AreEqual("int64", dateTimeField.Type.TypeName?.ToLowerInvariant());

            var dateTimeOffsetField = definition.Fields.Find(f => f.Name == "DateTimeOffsetField");
            Assert.IsNotNull(dateTimeOffsetField);
            Assert.AreEqual(CapnpSchemaReflection.TypeKind.Primitive, dateTimeOffsetField.Type.Kind);
            Assert.AreEqual("int64", dateTimeOffsetField.Type.TypeName?.ToLowerInvariant());

            var timeSpanField = definition.Fields.Find(f => f.Name == "TimeSpanField");
            Assert.IsNotNull(timeSpanField);
            Assert.AreEqual(CapnpSchemaReflection.TypeKind.Primitive, timeSpanField.Type.Kind);
            Assert.AreEqual("int64", timeSpanField.Type.TypeName?.ToLowerInvariant());
        }

        // Test class for DateTime testing
        public class DateTimeTestClass : ICapnpSerializable
        {
            public DateTime DateTimeField { get; set; }
            public DateTimeOffset DateTimeOffsetField { get; set; }
            public TimeSpan TimeSpanField { get; set; }

            public void Serialize(SerializerState state)
            {
                throw new NotImplementedException();
            }

            public void Deserialize(DeserializerState state)
            {
                throw new NotImplementedException();
            }
        }
    }
}
