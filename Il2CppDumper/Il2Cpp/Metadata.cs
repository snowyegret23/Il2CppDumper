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
        public Il2CppMethodSpec[] methodSpecs;
        public Il2CppGenericMethodFunctionsDefinitions[] genericMethodTable;
        public Il2CppTokenRangePair[] rgctxRanges;
        public int[] invokerIndices;
        public int[] staticConstructorTypeIndices;
        public Il2CppInlineArrayLength[] typeInlineArrays;
        private readonly Dictionary<Il2CppTypeDefinition, Il2CppGeneratedMethodTypeInfo> generatedMethods = new();

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
            if (version < 16 || (version > 31 && version != 35 && version != 38 && version != 39 &&
                (version < 104 || version > 108) && version != 110))
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
            nestedTypeIndices = ReadMetadataClassArray<int>(header.nestedTypesOffset, header.nestedTypesSize, header.nestedTypesCount);
            eventDefs = ReadMetadataClassArray<Il2CppEventDefinition>(header.eventsOffset, header.eventsSize, header.eventsCount);
            genericContainers = ReadMetadataClassArray<Il2CppGenericContainer>(header.genericContainersOffset, header.genericContainersSize, header.genericContainersCount);
            if (Version >= 106)
            {
                foreach (var container in genericContainers)
                {
                    container.type_argc = container.type_argc16;
                    container.is_method = container.is_method8;
                }
            }
            genericParameters = ReadMetadataClassArray<Il2CppGenericParameter>(header.genericParametersOffset, header.genericParametersSize, header.genericParametersCount);
            constraintIndices = ReadIndexArray(header.genericParameterConstraintsOffset, header.genericParameterConstraintsSize, header.genericParameterConstraintsCount, VariableIndexKind.Type);
            vtableMethods = ReadMetadataClassArray<uint>(header.vtableMethodsOffset, header.vtableMethodsSize, header.vtableMethodsCount);
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
            if (Version >= 104)
                typeInlineArrays = ReadMetadataClassArray<Il2CppInlineArrayLength>(header.typeInlineArraysOffset, header.typeInlineArraysSize, header.typeInlineArraysCount);
            if (Version >= 108)
            {
                methodSpecs = ReadMethodSpecs(header.methodSpecsOnGenericType, true, false)
                    .Concat(ReadMethodSpecs(header.genericMethodSpecsOnType, false, true))
                    .Concat(ReadMethodSpecs(header.methodSpecs, true, true)).ToArray();
                invokerIndices = ReadIndexArray(header.invokerIndices.offset, header.invokerIndices.size, header.invokerIndices.count, VariableIndexKind.Invoker);
                staticConstructorTypeIndices = ReadIndexArray(header.staticConstructorTypeIndices.offset, header.staticConstructorTypeIndices.size, header.staticConstructorTypeIndices.count, VariableIndexKind.TypeDefinition);
                rgctxRanges = ReadMetadataClassArray<Il2CppTokenRangePair>(header.rgctxRanges.offset, header.rgctxRanges.size, header.rgctxRanges.count);
                rgctxEntries = ReadMetadataClassArray<Il2CppRGCTXDefinition>(header.rgctxValues.offset, header.rgctxValues.size, header.rgctxValues.count);
                foreach (var entry in rgctxEntries)
                    entry.type_post29 = entry.type_post108;
            }
            if (Version >= 110)
                RestoreMetadataTokens();
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
            InferTypeWidth(header.fieldsSize, header.fieldsCount, Version >= 110 ? 4 : 8);
            SetIndexWidth(VariableIndexKind.Type, typeWidth ?? 4);

            if (Version >= 104)
            {
                SetIndexWidth(VariableIndexKind.Event, IndexWidthForCount(header.eventsCount));
                SetIndexWidth(VariableIndexKind.Property, IndexWidthForCount(header.propertiesCount));
                SetIndexWidth(VariableIndexKind.NestedType, IndexWidthForCount(header.nestedTypesCount));
                SetIndexWidth(VariableIndexKind.Interface, IndexWidthForCount(header.interfaceOffsetsCount));
            }
            if (Version >= 105)
                SetIndexWidth(VariableIndexKind.Method, IndexWidthForCount(header.methodsCount));
            if (Version >= 106)
            {
                SetIndexWidth(VariableIndexKind.GenericParameter, IndexWidthForCount(header.genericParametersCount));
                SetIndexWidth(VariableIndexKind.Field, IndexWidthForCount(header.fieldsCount));
                SetIndexWidth(VariableIndexKind.DefaultValueData, IndexWidthForCount(header.fieldAndParameterDefaultValueDataCount));
            }
            if (typeWidth == null && header.typeDefinitionsCount > 0)
            {
                var fixedSize = SizeOf(typeof(Il2CppTypeDefinition)) - 3 * sizeof(int);
                var variableSize = header.typeDefinitionsSize / header.typeDefinitionsCount - fixedSize;
                if (header.typeDefinitionsSize % header.typeDefinitionsCount != 0 || variableSize % 3 != 0)
                    throw new InvalidDataException("Cannot determine TypeIndex width from type definitions.");
                SetIndexWidth(VariableIndexKind.Type, variableSize / 3);
            }
            if (Version >= 108)
                InitializeGenericIndexWidths();

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

        private void InitializeGenericIndexWidths()
        {
            var methodWidth = GetIndexWidth(VariableIndexKind.Method);
            int? instWidth = null;
            InferInst(header.methodSpecsOnGenericType, 1);
            InferInst(header.genericMethodSpecsOnType, 1);
            InferInst(header.methodSpecs, 2);
            SetIndexWidth(VariableIndexKind.GenericInst, instWidth ?? 4);
            SetIndexWidth(VariableIndexKind.MethodSpec, IndexWidthForCount(checked(header.methodSpecsOnGenericType.count +
                header.genericMethodSpecsOnType.count + header.methodSpecs.count)));
            SetIndexWidth(VariableIndexKind.Invoker, SectionStride(header.invokerIndices) ?? 4);

            var plainStride = SectionStride(header.genericMethodFunctions);
            var adjustorStride = SectionStride(header.genericMethodFunctionsWithAdjustor);
            var commonWidth = GetIndexWidth(VariableIndexKind.MethodSpec) + GetIndexWidth(VariableIndexKind.Invoker);
            if (plainStride.HasValue)
                SetIndexWidth(VariableIndexKind.MethodPointer, plainStride.Value - commonWidth);
            if (plainStride.HasValue && adjustorStride.HasValue)
                SetIndexWidth(VariableIndexKind.AdjustorThunk, adjustorStride.Value - commonWidth - GetIndexWidth(VariableIndexKind.MethodPointer));

            void InferInst(Il2CppMetadataSection section, int fields)
            {
                var stride = SectionStride(section);
                if (!stride.HasValue)
                    return;
                var size = stride.Value - methodWidth;
                var width = size / fields;
                if (size % fields != 0 || (instWidth.HasValue && instWidth != width))
                    throw new InvalidDataException("Inconsistent generic instance index widths.");
                SetIndexWidth(VariableIndexKind.GenericInst, width);
                instWidth = width;
            }
        }

        private int? SectionStride(Il2CppMetadataSection section)
        {
            if (section.size < 0 || section.count < 0 || (ulong)section.offset + (ulong)section.size > Length ||
                (section.count == 0 ? section.size != 0 : section.size % section.count != 0))
                throw new InvalidDataException("Invalid metadata section size/count.");
            return section.count == 0 ? null : section.size / section.count;
        }

        private Il2CppMethodSpec[] ReadMethodSpecs(Il2CppMetadataSection section, bool classArgument, bool methodArgument)
        {
            var stride = GetIndexWidth(VariableIndexKind.Method) +
                GetIndexWidth(VariableIndexKind.GenericInst) * (classArgument && methodArgument ? 2 : 1);
            var count = ValidateSection(section.offset, section.size, section.count, stride, "method specs");
            Position = section.offset;
            var result = new Il2CppMethodSpec[count];
            for (var i = 0; i < count; i++)
                result[i] = new Il2CppMethodSpec
                {
                    methodDefinitionIndex = ReadIndex(VariableIndexKind.Method),
                    classIndexIndex = classArgument ? ReadIndex(VariableIndexKind.GenericInst) : -1,
                    methodIndexIndex = methodArgument ? ReadIndex(VariableIndexKind.GenericInst) : -1
                };
            return result;
        }

        private Il2CppGenericMethodFunctionsDefinitions[] ReadGenericMethods(Il2CppMetadataSection section, bool adjustor)
        {
            var stride = GetIndexWidth(VariableIndexKind.MethodSpec) + GetIndexWidth(VariableIndexKind.MethodPointer) +
                GetIndexWidth(VariableIndexKind.Invoker) + (adjustor ? GetIndexWidth(VariableIndexKind.AdjustorThunk) : 0);
            var count = ValidateSection(section.offset, section.size, section.count, stride, "generic methods");
            Position = section.offset;
            var result = new Il2CppGenericMethodFunctionsDefinitions[count];
            for (var i = 0; i < count; i++)
                result[i] = new Il2CppGenericMethodFunctionsDefinitions
                {
                    genericMethodIndex = ReadIndex(VariableIndexKind.MethodSpec),
                    indices = new Il2CppGenericMethodIndices
                    {
                        methodIndex = ReadIndex(VariableIndexKind.MethodPointer),
                        invokerIndex = ReadIndex(VariableIndexKind.Invoker),
                        adjustorThunk = adjustor ? ReadIndex(VariableIndexKind.AdjustorThunk) : -1
                    }
                };
            return result;
        }

        public Il2CppGenericMethodFunctionsDefinitions[] ReadGenericMethodTable(int methodPointerCount, int invokerCount)
        {
            SetIndexWidth(VariableIndexKind.MethodPointer, IndexWidthForCount(methodPointerCount));
            SetIndexWidth(VariableIndexKind.Invoker, IndexWidthForCount(invokerCount));
            if (header.invokerIndices.count > 0 && SectionStride(header.invokerIndices) != GetIndexWidth(VariableIndexKind.Invoker))
                throw new InvalidDataException("Metadata and binary invoker index widths disagree.");
            var adjustorStride = SectionStride(header.genericMethodFunctionsWithAdjustor);
            if (adjustorStride.HasValue)
                SetIndexWidth(VariableIndexKind.AdjustorThunk, adjustorStride.Value - GetIndexWidth(VariableIndexKind.MethodSpec) -
                    GetIndexWidth(VariableIndexKind.MethodPointer) - GetIndexWidth(VariableIndexKind.Invoker));
            genericMethodTable = ReadGenericMethods(header.genericMethodFunctions, false)
                .Concat(ReadGenericMethods(header.genericMethodFunctionsWithAdjustor, true)).ToArray();
            return genericMethodTable;
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

        public int GetMethodIndex(Il2CppTypeDefinition type, int indexInType)
        {
            if (indexInType < 0 || indexInType >= type.method_count)
                throw new InvalidDataException("Method index is outside its declaring type.");
            if (generatedMethods.TryGetValue(type, out var info))
            {
                var ordinaryCount = type.method_count - info.generatedMethodCount;
                if (indexInType >= ordinaryCount)
                    return checked(info.generatedMethodStart + indexInType - ordinaryCount);
            }
            return checked(type.methodStart + indexInType);
        }

        private void RestoreMetadataTokens()
        {
            var section = header.generatedMethodTypeInfos;
            var infos = ReadMetadataClassArray<Il2CppGeneratedMethodTypeInfo>(section.offset, section.size, section.count);
            section = header.generatedMethodTokens;
            var tokenCount = ValidateSection(section.offset, section.size, section.count, 4, "generated method tokens");
            var tokens = ReadClassArray<uint>(section.offset, tokenCount);
            var generatedStart = methodDefs.Length - tokens.Length;
            foreach (var info in infos)
            {
                var type = typeDefs[info.typeIndex];
                if ((type.bitfield & (1u << 19)) == 0 || info.generatedMethodCount < 0 ||
                    info.generatedMethodCount > type.method_count || info.generatedMethodStart < generatedStart ||
                    (long)info.generatedMethodStart + info.generatedMethodCount > methodDefs.Length)
                    throw new InvalidDataException("Invalid generated method range.");
                generatedMethods.Add(type, info);
            }
            foreach (var image in imageDefs)
            {
                for (var i = image.typeStart; i < (long)image.typeStart + image.typeCount; i++)
                {
                    var type = typeDefs[i];
                    type.token = Token(0x02000000, i, image.typeStart);
                    for (var j = 0; j < type.field_count; j++)
                        fieldDefs[type.fieldStart + j].token = Token(0x04000000, type.fieldStart + j, image.fieldStart);
                    for (var j = 0; j < type.property_count; j++)
                        propertyDefs[type.propertyStart + j].token = Token(0x17000000, type.propertyStart + j, image.propertyStart);
                    for (var j = 0; j < type.event_count; j++)
                        eventDefs[type.eventStart + j].token = Token(0x14000000, type.eventStart + j, image.eventStart);
                    if ((type.bitfield & (1u << 19)) != 0 && !generatedMethods.ContainsKey(type))
                        throw new InvalidDataException("Missing generated method information.");
                    for (var j = 0; j < type.method_count; j++)
                    {
                        var index = GetMethodIndex(type, j);
                        methodDefs[index].token = index >= generatedStart ? tokens[index - generatedStart]
                            : Token(0x06000000, index, image.methodStart);
                    }
                }
            }

            static uint Token(uint kind, int index, int start)
            {
                var row = (long)index - start + 1;
                if (row <= 0 || row > 0x00FFFFFF)
                    throw new InvalidDataException("Metadata token row is outside its image.");
                return kind | (uint)row;
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

        public double GetBinaryVersion()
        {
            if (Version != 106 && Version != 107)
                return Version;
            var savedPosition = Position;
            try
            {
                foreach (var range in attributeDataRanges)
                {
                    if (range.startOffset >= header.attributeDataSize)
                        continue;
                    Position = header.attributeDataOffset + (ulong)range.startOffset;
                    var count = ReadCompressedUInt32();
                    if (count == 0)
                        continue;
                    if (Position + (ulong)count * 4 > (ulong)header.attributeDataOffset + (ulong)header.attributeDataSize)
                        throw new InvalidDataException("Invalid custom attribute constructor table.");
                    var usage = GetEncodedIndexType(ReadUInt32());
                    if (usage == 3 || usage == 6)
                        return 106;
                    if (usage == 2 || usage == 5)
                        return 106.1;
                    throw new InvalidDataException("Unknown metadata usage encoding in v106/v107.");
                }
                throw new NotSupportedException("Cannot distinguish v106/v107 binary layouts without constructor metadata. Set ForceIl2CppVersion and ForceVersion to 106 or 106.1 in config.json.");
            }
            finally
            {
                Position = savedPosition;
            }
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
            if (type.IsPrimitive)
                return GetPrimitiveTypeSize(type.Name);
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
