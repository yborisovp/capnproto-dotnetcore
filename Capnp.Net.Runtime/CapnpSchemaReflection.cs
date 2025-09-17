using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;

namespace Capnp
{
    /// <summary>
    /// Provides functionality to generate capnp schema from C# classes using reflection.
    /// This class analyzes C# types and generates corresponding capnp schema definitions.
    /// </summary>
    public static class CapnpSchemaReflection
    {
        /// <summary>
        /// Represents a capnp schema definition generated from a C# type.
        /// </summary>
        public class SchemaDefinition
        {
            public string Name { get; set; } = string.Empty;
            public ulong TypeId { get; set; }
            public TypeKind Kind { get; set; }
            public List<FieldDefinition> Fields { get; set; } = new List<FieldDefinition>();
            public List<EnumValue> EnumValues { get; set; } = new List<EnumValue>();
            public string Namespace { get; set; } = string.Empty;
            public List<SchemaDefinition> NestedTypes { get; set; } = new List<SchemaDefinition>();
        }

        /// <summary>
        /// Represents a field in a capnp struct.
        /// </summary>
        public class FieldDefinition
        {
            public string Name { get; set; } = string.Empty;
            public int Index { get; set; }
            public CapnpType Type { get; set; } = new CapnpType();
            public bool IsOptional { get; set; }
            public object? DefaultValue { get; set; }
        }

        /// <summary>
        /// Represents an enum value.
        /// </summary>
        public class EnumValue
        {
            public string Name { get; set; } = string.Empty;
            public int Value { get; set; }
        }

        /// <summary>
        /// Represents a capnp type.
        /// </summary>
        public class CapnpType
        {
            public TypeKind Kind { get; set; }
            public CapnpType? ElementType { get; set; }
            public string? TypeName { get; set; }
            public ulong TypeId { get; set; }
        }

        /// <summary>
        /// Type kinds in capnp schema.
        /// </summary>
        public enum TypeKind
        {
            Struct,
            Enum,
            Interface,
            List,
            Primitive,
            Text,
            Data,
            Void
        }

        /// <summary>
        /// Generates capnp schema definition from a C# type.
        /// </summary>
        /// <param name="type">The C# type to analyze</param>
        /// <returns>Schema definition</returns>
        public static SchemaDefinition GenerateSchema(Type type)
        {
            if (type == null)
                throw new ArgumentNullException(nameof(type));

            var definition = new SchemaDefinition
            {
                Name = type.Name,
                Namespace = type.Namespace ?? ""
            };

            // Extract TypeId from attribute
            var typeIdAttr = type.GetCustomAttribute<TypeIdAttribute>();
            if (typeIdAttr != null)
            {
                definition.TypeId = typeIdAttr.Id;
            }
            else
            {
                // Generate a pseudo-random TypeId if not specified
                definition.TypeId = GenerateTypeId(type);
            }

            // Determine type kind
            if (type.IsEnum)
            {
                definition.Kind = TypeKind.Enum;
                AnalyzeEnum(type, definition);
            }
            else if (type.IsInterface)
            {
                definition.Kind = TypeKind.Interface;
                AnalyzeInterface(type, definition);
            }
            else if (typeof(ICapnpSerializable).IsAssignableFrom(type))
            {
                definition.Kind = TypeKind.Struct;
                AnalyzeStruct(type, definition);
            }
            else
            {
                throw new ArgumentException($"Type {type.Name} is not supported for capnp schema generation. " +
                    "Supported types are: enums, interfaces, and classes implementing ICapnpSerializable.");
            }

            return definition;
        }

        /// <summary>
        /// Generates capnp schema text from a C# type.
        /// </summary>
        /// <param name="type">The C# type to analyze</param>
        /// <returns>Capnp schema as text</returns>
        public static string GenerateSchemaText(Type type)
        {
            var definition = GenerateSchema(type);
            return GenerateSchemaText(definition);
        }

        /// <summary>
        /// Generates capnp schema text from a schema definition.
        /// </summary>
        /// <param name="definition">The schema definition</param>
        /// <returns>Capnp schema as text</returns>
        public static string GenerateSchemaText(SchemaDefinition definition)
        {
            var sb = new StringBuilder();

            // Add namespace if specified
            if (!string.IsNullOrEmpty(definition.Namespace))
            {
                sb.AppendLine($"$namespace(\"{definition.Namespace}\");");
                sb.AppendLine();
            }

            // Generate type definition
            switch (definition.Kind)
            {
                case TypeKind.Struct:
                    GenerateStructSchema(sb, definition);
                    break;
                case TypeKind.Enum:
                    GenerateEnumSchema(sb, definition);
                    break;
                case TypeKind.Interface:
                    GenerateInterfaceSchema(sb, definition);
                    break;
            }

            return sb.ToString();
        }

        private static void AnalyzeEnum(Type type, SchemaDefinition definition)
        {
            var enumValues = Enum.GetValues(type);
            var enumNames = Enum.GetNames(type);

            for (int i = 0; i < enumValues.Length; i++)
            {
                definition.EnumValues.Add(new EnumValue
                {
                    Name = enumNames[i],
                    Value = Convert.ToInt32(enumValues.GetValue(i))
                });
            }
        }

        private static void AnalyzeInterface(Type type, SchemaDefinition definition)
        {
            // Analyze interface methods
            var methods = type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly);

            foreach (var method in methods)
            {
                // For now, we'll create a simple field for each method
                // In a full implementation, you'd want to analyze method signatures
                // and create proper capnp interface definitions
                var field = new FieldDefinition
                {
                    Name = method.Name,
                    Index = definition.Fields.Count,
                    Type = new CapnpType { Kind = TypeKind.Void }
                };
                definition.Fields.Add(field);
            }
        }

        private static void AnalyzeStruct(Type type, SchemaDefinition definition)
        {
            var properties = type.GetProperties(BindingFlags.Public | BindingFlags.Instance);
            var fields = type.GetFields(BindingFlags.Public | BindingFlags.Instance);

            int fieldIndex = 0;

            // Analyze properties
            foreach (var prop in properties)
            {
                if (prop.CanRead && prop.CanWrite)
                {
                    var field = new FieldDefinition
                    {
                        Name = prop.Name,
                        Index = fieldIndex++,
                        Type = MapCSharpTypeToCapnp(prop.PropertyType),
                        IsOptional = IsNullableType(prop.PropertyType)
                    };
                    definition.Fields.Add(field);
                }
            }

            // Analyze fields
            foreach (var field in fields)
            {
                var fieldDef = new FieldDefinition
                {
                    Name = field.Name,
                    Index = fieldIndex++,
                    Type = MapCSharpTypeToCapnp(field.FieldType),
                    IsOptional = IsNullableType(field.FieldType)
                };
                definition.Fields.Add(fieldDef);
            }
        }

        private static CapnpType MapCSharpTypeToCapnp(Type csharpType)
        {
            // Handle nullable types
            var underlyingType = Nullable.GetUnderlyingType(csharpType) ?? csharpType;

            // Handle generic types (like List<T>)
            if (csharpType.IsGenericType)
            {
                var genericTypeDef = csharpType.GetGenericTypeDefinition();
                if (genericTypeDef == typeof(IReadOnlyList<>) ||
                    genericTypeDef == typeof(List<>) ||
                    genericTypeDef == typeof(IList<>))
                {
                    var elementType = csharpType.GetGenericArguments()[0];
                    return new CapnpType
                    {
                        Kind = TypeKind.List,
                        ElementType = MapCSharpTypeToCapnp(elementType)
                    };
                }
            }

            // Map primitive types
            if (underlyingType == typeof(bool))
                return new CapnpType { Kind = TypeKind.Primitive, TypeName = "Bool" };
            if (underlyingType == typeof(sbyte))
                return new CapnpType { Kind = TypeKind.Primitive, TypeName = "Int8" };
            if (underlyingType == typeof(byte))
                return new CapnpType { Kind = TypeKind.Primitive, TypeName = "UInt8" };
            if (underlyingType == typeof(short))
                return new CapnpType { Kind = TypeKind.Primitive, TypeName = "Int16" };
            if (underlyingType == typeof(ushort))
                return new CapnpType { Kind = TypeKind.Primitive, TypeName = "UInt16" };
            if (underlyingType == typeof(int))
                return new CapnpType { Kind = TypeKind.Primitive, TypeName = "Int32" };
            if (underlyingType == typeof(uint))
                return new CapnpType { Kind = TypeKind.Primitive, TypeName = "UInt32" };
            if (underlyingType == typeof(long))
                return new CapnpType { Kind = TypeKind.Primitive, TypeName = "Int64" };
            if (underlyingType == typeof(ulong))
                return new CapnpType { Kind = TypeKind.Primitive, TypeName = "UInt64" };
            if (underlyingType == typeof(float))
                return new CapnpType { Kind = TypeKind.Primitive, TypeName = "Float32" };
            if (underlyingType == typeof(double))
                return new CapnpType { Kind = TypeKind.Primitive, TypeName = "Float64" };
            if (underlyingType == typeof(string))
                return new CapnpType { Kind = TypeKind.Text };
            if (underlyingType == typeof(byte[]))
                return new CapnpType { Kind = TypeKind.Data };
            if (underlyingType == typeof(DateTime))
                return new CapnpType { Kind = TypeKind.Primitive, TypeName = "Int64" }; // Unix timestamp
            if (underlyingType == typeof(DateTimeOffset))
                return new CapnpType { Kind = TypeKind.Primitive, TypeName = "Int64" }; // Unix timestamp
            if (underlyingType == typeof(TimeSpan))
                return new CapnpType { Kind = TypeKind.Primitive, TypeName = "Int64" }; // Ticks or seconds

            // Handle enums
            if (underlyingType.IsEnum)
            {
                return new CapnpType
                {
                    Kind = TypeKind.Enum,
                    TypeName = underlyingType.Name,
                    TypeId = GetTypeId(underlyingType)
                };
            }

            // Handle custom types (assuming they implement ICapnpSerializable)
            if (typeof(ICapnpSerializable).IsAssignableFrom(underlyingType))
            {
                return new CapnpType
                {
                    Kind = TypeKind.Struct,
                    TypeName = underlyingType.Name,
                    TypeId = GetTypeId(underlyingType)
                };
            }

            // Default to void for unsupported types
            return new CapnpType { Kind = TypeKind.Void };
        }

        private static bool IsNullableType(Type type)
        {
            return Nullable.GetUnderlyingType(type) != null;
        }

        private static ulong GetTypeId(Type type)
        {
            var typeIdAttr = type.GetCustomAttribute<TypeIdAttribute>();
            return typeIdAttr?.Id ?? GenerateTypeId(type);
        }

        private static ulong GenerateTypeId(Type type)
        {
            // Generate a pseudo-random TypeId based on type name hash
            var hash = type.FullName?.GetHashCode() ?? type.Name.GetHashCode();
            return (ulong)(Math.Abs(hash) + (long)Math.Pow(2, 32));
        }

        private static void GenerateStructSchema(StringBuilder sb, SchemaDefinition definition)
        {
            sb.AppendLine($"struct {definition.Name} @0x{definition.TypeId:X} {{");

            foreach (var field in definition.Fields)
            {
                var optional = field.IsOptional ? "?" : "";
                var typeName = GetCapnpTypeName(field.Type);
                sb.AppendLine($"  {typeName}{optional} {field.Name.ToLowerInvariant()} @{field.Index};");
            }

            sb.AppendLine("}");
        }

        private static void GenerateEnumSchema(StringBuilder sb, SchemaDefinition definition)
        {
            sb.AppendLine($"enum {definition.Name} @0x{definition.TypeId:X} {{");

            foreach (var enumValue in definition.EnumValues)
            {
                sb.AppendLine($"  {enumValue.Name.ToLowerInvariant()} @{enumValue.Value};");
            }

            sb.AppendLine("}");
        }

        private static void GenerateInterfaceSchema(StringBuilder sb, SchemaDefinition definition)
        {
            sb.AppendLine($"interface {definition.Name} @0x{definition.TypeId:X} {{");

            foreach (var field in definition.Fields)
            {
                sb.AppendLine($"  {field.Name.ToLowerInvariant()}() -> ();");
            }

            sb.AppendLine("}");
        }

        private static string GetCapnpTypeName(CapnpType type)
        {
            switch (type.Kind)
            {
                case TypeKind.Primitive:
                    return type.TypeName?.ToLowerInvariant() ?? "void";
                case TypeKind.Text:
                    return "Text";
                case TypeKind.Data:
                    return "Data";
                case TypeKind.List:
                    return $"List({GetCapnpTypeName(type.ElementType)})";
                case TypeKind.Struct:
                case TypeKind.Enum:
                    return type.TypeName ?? "Void";
                case TypeKind.Void:
                default:
                    return "Void";
            }
        }
    }
}
