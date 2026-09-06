using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;

namespace Il2CppDumper
{
    public sealed class Metadata : BinaryStream
    {
        public Il2CppGlobalMetadataHeader header;
        public Il2CppImageDefinition[] imageDefs;
        public Il2CppAssemblyDefinition[] assemblyDefs;
        public Il2CppTypeDefinition[] typeDefs;
        public Il2CppMethodDefinition[] methodDefs;
        public Il2CppParameterDefinition[] parameterDefs;
        public Il2CppFieldDefinition[] fieldDefs;
        private readonly Dictionary<int, Il2CppFieldDefaultValue> fieldDefaultValuesDic;
        private readonly Dictionary<int, Il2CppParameterDefaultValue> parameterDefaultValuesDic;
        public Il2CppPropertyDefinition[] propertyDefs;
        public Il2CppCustomAttributeTypeRange[] attributeTypeRanges;
        public Il2CppCustomAttributeDataRange[] attributeDataRanges;
        private readonly Dictionary<Il2CppImageDefinition, Dictionary<uint, int>> attributeTypeRangesDic;
        public Il2CppStringLiteral[] stringLiterals;
        private readonly Il2CppMetadataUsageList[] metadataUsageLists;
        private readonly Il2CppMetadataUsagePair[] metadataUsagePairs;
        public int[] attributeTypes;
        public int[] interfaceIndices;
        public Dictionary<Il2CppMetadataUsage, SortedDictionary<uint, uint>> metadataUsageDic;
        public long metadataUsagesCount;
        public int[] nestedTypeIndices;
        public Il2CppEventDefinition[] eventDefs;
        public Il2CppGenericContainer[] genericContainers;
        public Il2CppFieldRef[] fieldRefs;
        public Il2CppGenericParameter[] genericParameters;
        public int[] constraintIndices;
        public uint[] vtableMethods;
        public Il2CppRGCTXDefinition[] rgctxEntries;

        private readonly Dictionary<uint, string> stringCache = new();

        public Metadata(Stream stream) : base(stream)
        {
            var sanity = ReadUInt32();
            if (sanity != 0xFAB11BAF)
            {
                throw new InvalidDataException("ERROR: Metadata file supplied is not valid metadata file.");
            }
            var version = ReadInt32();
            if (version < 0 || version > 1000)
            {
                throw new InvalidDataException("ERROR: Metadata file supplied is not valid metadata file.");
            }
            if (version < 16 || (version > 31 && version != 35 && version != 38 && version != 39))
            {
                throw new NotSupportedException($"ERROR: Metadata file supplied is not a supported version[{version}].");
            }
            Version = version;
            header = ReadClass<Il2CppGlobalMetadataHeader>(0);
            InitializeIndexWidths();
            if (version == 24)
            {
                if (header.stringLiteralOffset == 264)
                {
                    Version = 24.2;
                    header = ReadClass<Il2CppGlobalMetadataHeader>(0);
                }
                else
                {
                    imageDefs = ReadClassArray<Il2CppImageDefinition>(header.imagesOffset,
                        header.imagesSize / SizeOf(typeof(Il2CppImageDefinition)));
                    if (imageDefs.Any(x => x.token != 1))
                    {
                        Version = 24.1;
                    }
                }
            }
            imageDefs = ReadMetadataClassArray<Il2CppImageDefinition>(header.imagesOffset, header.imagesSize, header.imagesCount);
            if (Version == 24.2 && header.assembliesSize / 68 < imageDefs.Length)
            {
                Version = 24.4;
            }
            var v241Plus = false;
            if (Version == 24.1 && header.assembliesSize / 64 == imageDefs.Length)
            {
                v241Plus = true;
            }
            if (v241Plus)
            {
                Version = 24.4;
            }
            assemblyDefs = ReadMetadataClassArray<Il2CppAssemblyDefinition>(header.assembliesOffset, header.assembliesSize, header.assembliesCount);
            if (v241Plus)
            {
                Version = 24.1;
            }
            typeDefs = ReadMetadataClassArray<Il2CppTypeDefinition>(header.typeDefinitionsOffset, header.typeDefinitionsSize, header.typeDefinitionsCount);
            RestoreEnumTypes();
            methodDefs = ReadMetadataClassArray<Il2CppMethodDefinition>(header.methodsOffset, header.methodsSize, header.methodsCount);
            parameterDefs = ReadMetadataClassArray<Il2CppParameterDefinition>(header.parametersOffset, header.parametersSize, header.parametersCount);
            fieldDefs = ReadMetadataClassArray<Il2CppFieldDefinition>(header.fieldsOffset, header.fieldsSize, header.fieldsCount);
            var fieldDefaultValues = ReadMetadataClassArray<Il2CppFieldDefaultValue>(header.fieldDefaultValuesOffset, header.fieldDefaultValuesSize, header.fieldDefaultValuesCount);
            var parameterDefaultValues = ReadMetadataClassArray<Il2CppParameterDefaultValue>(header.parameterDefaultValuesOffset, header.parameterDefaultValuesSize, header.parameterDefaultValuesCount);
            fieldDefaultValuesDic = fieldDefaultValues.ToDictionary(x => x.fieldIndex);
            parameterDefaultValuesDic = parameterDefaultValues.ToDictionary(x => x.parameterIndex);
            propertyDefs = ReadMetadataClassArray<Il2CppPropertyDefinition>(header.propertiesOffset, header.propertiesSize, header.propertiesCount);
            interfaceIndices = ReadIndexArray(header.interfacesOffset, header.interfacesSize, header.interfacesCount, VariableIndexKind.Type);
            nestedTypeIndices = ReadClassArray<int>(header.nestedTypesOffset, header.nestedTypesSize / 4);
            eventDefs = ReadMetadataClassArray<Il2CppEventDefinition>(header.eventsOffset, header.eventsSize, header.eventsCount);
            genericContainers = ReadMetadataClassArray<Il2CppGenericContainer>(header.genericContainersOffset, header.genericContainersSize, header.genericContainersCount);
            genericParameters = ReadMetadataClassArray<Il2CppGenericParameter>(header.genericParametersOffset, header.genericParametersSize, header.genericParametersCount);
            constraintIndices = ReadIndexArray(header.genericParameterConstraintsOffset, header.genericParameterConstraintsSize, header.genericParameterConstraintsCount, VariableIndexKind.Type);
            vtableMethods = ReadClassArray<uint>(header.vtableMethodsOffset, header.vtableMethodsSize / 4);
            stringLiterals = ReadMetadataClassArray<Il2CppStringLiteral>(header.stringLiteralOffset, header.stringLiteralSize, header.stringLiteralCount);
            if (Version > 16)
            {
                fieldRefs = ReadMetadataClassArray<Il2CppFieldRef>(header.fieldRefsOffset, header.fieldRefsSize, header.fieldRefsCount);
                if (Version < 27)
                {
                    metadataUsageLists = ReadMetadataClassArray<Il2CppMetadataUsageList>(header.metadataUsageListsOffset, header.metadataUsageListsCount);
                    metadataUsagePairs = ReadMetadataClassArray<Il2CppMetadataUsagePair>(header.metadataUsagePairsOffset, header.metadataUsagePairsCount);

                    ProcessingMetadataUsage();
                }
            }
            if (Version > 20 && Version < 29)
            {
                attributeTypeRanges = ReadMetadataClassArray<Il2CppCustomAttributeTypeRange>(header.attributesInfoOffset, header.attributesInfoCount);
                attributeTypes = ReadClassArray<int>(header.attributeTypesOffset, header.attributeTypesCount / 4);
            }
            if (Version >= 29)
            {
                attributeDataRanges = ReadMetadataClassArray<Il2CppCustomAttributeDataRange>(header.attributeDataRangeOffset, header.attributeDataRangeSize, header.attributeDataRangeCount);
            }
            if (Version > 24)
            {
                attributeTypeRangesDic = new Dictionary<Il2CppImageDefinition, Dictionary<uint, int>>();
                foreach (var imageDef in imageDefs)
                {
                    var dic = new Dictionary<uint, int>();
                    attributeTypeRangesDic[imageDef] = dic;
                    var end = imageDef.customAttributeStart + imageDef.customAttributeCount;
                    for (int i = imageDef.customAttributeStart; i < end; i++)
                    {
                        if (Version >= 29)
                        {
                            dic.Add(attributeDataRanges[i].token, i);
                        }
                        else
                        {
                            dic.Add(attributeTypeRanges[i].token, i);
                        }
                    }
                }
            }
            if (Version <= 24.1)
            {
                rgctxEntries = ReadMetadataClassArray<Il2CppRGCTXDefinition>(header.rgctxEntriesOffset, header.rgctxEntriesCount);
            }
        }

        private void InitializeIndexWidths()
        {
            if (Version < 38)
                return;

            SetIndexWidth(VariableIndexKind.TypeDefinition, IndexWidthForCount(header.typeDefinitionsCount));
            SetIndexWidth(VariableIndexKind.GenericContainer, IndexWidthForCount(header.genericContainersCount));
            if (Version >= 39)
                SetIndexWidth(VariableIndexKind.Parameter, IndexWidthForCount(header.parametersCount));

            int? typeWidth = null;
            InferTypeWidth(header.interfaceOffsetsSize, header.interfaceOffsetsCount, 4);
            InferTypeWidth(header.parametersSize, header.parametersCount, 8);
            InferTypeWidth(header.fieldsSize, header.fieldsCount, 8);
            if (typeWidth == null && header.typeDefinitionsCount > 0)
                throw new InvalidDataException("Cannot determine the metadata TypeIndex width from empty sections.");
            SetIndexWidth(VariableIndexKind.Type, typeWidth ?? 4);

            void InferTypeWidth(int size, int count, int fixedSize)
            {
                if (size < 0 || count < 0 || (count == 0 && size != 0))
                    throw new InvalidDataException("Invalid metadata section size/count.");
                if (count == 0)
                    return;
                var width = size / count - fixedSize;
                if (size % count != 0 || (width != 1 && width != 2 && width != 4) ||
                    (typeWidth.HasValue && typeWidth != width))
                    throw new InvalidDataException("Inconsistent metadata TypeIndex widths.");
                typeWidth = width;
            }
        }

        private static int IndexWidthForCount(int count)
        {
            if (count < 0)
                throw new InvalidDataException($"Invalid metadata count: {count}.");
            return count <= byte.MaxValue ? 1 : count <= ushort.MaxValue ? 2 : 4;
        }

        private void RestoreEnumTypes()
        {
            if (Version < 35 || !typeDefs.Any(t => t.IsEnum))
                return;
            var enumType = typeDefs.Single(t => GetStringFromIndex(t.nameIndex) == "Enum" &&
                GetStringFromIndex(t.namespaceIndex) == "System");
            foreach (var type in typeDefs.Where(t => t.IsEnum))
            {
                type.elementTypeIndex = type.parentIndex;
                type.parentIndex = enumType.byvalTypeIndex;
            }
        }

        private int ValidateSection(uint addr, int size, int count, int elementSize, string name)
        {
            if (size < 0 || (ulong)addr + (ulong)size > Length || elementSize <= 0 ||
                size % elementSize != 0 || (Version >= 38 && (count < 0 || (long)count * elementSize != size)))
                throw new InvalidDataException($"Invalid {name} section: offset={addr}, size={size}, count={count}, stride={elementSize}.");
            return size / elementSize;
        }

        private int[] ReadIndexArray(uint addr, int size, int count, VariableIndexKind kind)
        {
            var length = ValidateSection(addr, size, count, GetIndexWidth(kind), kind.ToString());
            Position = addr;
            var values = new int[length];
            for (var i = 0; i < values.Length; i++)
                values[i] = ReadIndex(kind);
            return values;
        }

        private T[] ReadMetadataClassArray<T>(uint addr, int size, int count = 0) where T : new()
        {
            var length = ValidateSection(addr, size, count, SizeOf(typeof(T)), typeof(T).Name);
            return ReadClassArray<T>(addr, length);
        }

        public bool GetFieldDefaultValueFromIndex(int index, out Il2CppFieldDefaultValue value)
        {
            return fieldDefaultValuesDic.TryGetValue(index, out value);
        }

        public bool GetParameterDefaultValueFromIndex(int index, out Il2CppParameterDefaultValue value)
        {
            return parameterDefaultValuesDic.TryGetValue(index, out value);
        }

        public uint GetDefaultValueFromIndex(int index)
        {
            return (uint)(header.fieldAndParameterDefaultValueDataOffset + index);
        }

        public string GetStringFromIndex(uint index)
        {
            if (!stringCache.TryGetValue(index, out var result))
            {
                result = ReadStringToNull(header.stringOffset + index);
                stringCache.Add(index, result);
            }
            return result;
        }

        public int GetCustomAttributeIndex(Il2CppImageDefinition imageDef, int customAttributeIndex, uint token)
        {
            if (Version > 24)
            {
                if (attributeTypeRangesDic[imageDef].TryGetValue(token, out var index))
                {
                    return index;
                }
                else
                {
                    return -1;
                }
            }
            else
            {
                return customAttributeIndex;
            }
        }

        public string GetStringLiteralFromIndex(uint index)
        {
            var stringLiteral = stringLiterals[index];
            var start = stringLiteral.dataIndex;
            var end = Version < 35 ? (long)start + stringLiteral.length
                : index + 1 < stringLiterals.Length ? stringLiterals[index + 1].dataIndex
                : header.stringLiteralDataSize;
            if (start < 0 || end < start || end > header.stringLiteralDataSize ||
                (ulong)header.stringLiteralDataOffset + (ulong)end > Length)
                throw new InvalidDataException($"Invalid string literal range at index {index}.");
            Position = header.stringLiteralDataOffset + (ulong)start;
            return Encoding.UTF8.GetString(ReadBytes(checked((int)(end - start))));
        }

        private void ProcessingMetadataUsage()
        {
            metadataUsageDic = new Dictionary<Il2CppMetadataUsage, SortedDictionary<uint, uint>>();
            for (uint i = 1; i <= 6; i++)
            {
                metadataUsageDic[(Il2CppMetadataUsage)i] = new SortedDictionary<uint, uint>();
            }
            foreach (var metadataUsageList in metadataUsageLists)
            {
                for (int i = 0; i < metadataUsageList.count; i++)
                {
                    var offset = metadataUsageList.start + i;
                    if (offset >= metadataUsagePairs.Length)
                    {
                        continue;
                    }
                    var metadataUsagePair = metadataUsagePairs[offset];
                    var usage = GetEncodedIndexType(metadataUsagePair.encodedSourceIndex);
                    var decodedIndex = GetDecodedMethodIndex(metadataUsagePair.encodedSourceIndex);
                    metadataUsageDic[(Il2CppMetadataUsage)usage][metadataUsagePair.destinationIndex] = decodedIndex;
                }
            }
            //metadataUsagesCount = metadataUsagePairs.Max(x => x.destinationIndex) + 1;
            metadataUsagesCount = metadataUsageDic.Max(x => x.Value.Select(y => y.Key).DefaultIfEmpty().Max()) + 1;
        }

        public static uint GetEncodedIndexType(uint index)
        {
            return (index & 0xE0000000) >> 29;
        }

        public uint GetDecodedMethodIndex(uint index)
        {
            if (Version >= 27)
            {
                return (index & 0x1FFFFFFEU) >> 1;
            }
            return index & 0x1FFFFFFFU;
        }

        public int SizeOf(Type type)
        {
            var size = 0;
            foreach (var i in type.GetFields())
            {
                var attributes = i.GetCustomAttributes<VersionAttribute>().ToArray();
                if (attributes.Length > 0)
                {
                    if (!attributes.Any(attr => Version >= attr.Min && Version <= attr.Max))
                        continue;
                }
                var fieldType = i.FieldType;
                var indexAttribute = i.GetCustomAttribute<VariableIndexAttribute>();
                if (indexAttribute != null)
                {
                    size += GetIndexWidth(indexAttribute.Kind);
                }
                else if (fieldType.IsPrimitive)
                {
                    size += GetPrimitiveTypeSize(fieldType.Name);
                }
                else if (fieldType.IsEnum)
                {
                    var e = fieldType.GetField("value__").FieldType;
                    size += GetPrimitiveTypeSize(e.Name);
                }
                else if (fieldType.IsArray)
                {
                    var arrayLengthAttribute = i.GetCustomAttribute<ArrayLengthAttribute>();
                    size += arrayLengthAttribute.Length * GetPrimitiveTypeSize(fieldType.GetElementType().Name);
                }
                else
                {
                    size += SizeOf(fieldType);
                }
            }
            return size;

            static int GetPrimitiveTypeSize(string name)
            {
                return name switch
                {
                    "Int32" or "UInt32" => 4,
                    "Int16" or "UInt16" => 2,
                    "Byte" or "SByte" => 1,
                    "Int64" or "UInt64" => 8,
                    _ => throw new NotSupportedException($"Unsupported metadata primitive: {name}."),
                };
            }
        }
    }
}
