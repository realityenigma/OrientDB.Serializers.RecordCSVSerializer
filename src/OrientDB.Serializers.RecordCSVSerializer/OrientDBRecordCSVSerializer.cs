using OrientDB.Core.Abstractions;
using System;
using System.Collections.Generic;
using System.Linq;
using OrientDB.Core;
using System.Text;
using System.Reflection;
using OrientDB.Serializers.RecordCSVSerializer.Extensions;
using System.Collections;
using System.Globalization;

namespace OrientDB.Serializers.RecordCSVSerializer
{
    public class OrientDBRecordCSVSerializer : IOrientDBRecordSerializer<byte[]>
    {
        public OrientDBRecordFormat RecordFormat
        {
            get
            {
                return OrientDBRecordFormat.CSV;
            }
        }

        public OrientDBRecordCSVSerializer()
        {

        }

        public IEnumerable<TResultType> Deserialize<TResultType>(byte[] data) where TResultType : IOrientDBEntity
        {
            var recordString = BinarySerializer.ToString(data).Trim();

            return Deserialize<TResultType>(recordString);
        }

        internal IEnumerable<TResultType> Deserialize<TResultType>(string recordString) where TResultType : IOrientDBEntity
        {
            TResultType entity = Activator.CreateInstance<TResultType>();

            int atIndex = recordString.IndexOf('@');
            int colonIndex = recordString.IndexOf(':');
            int index = 0;

            // parse class name
            if ((atIndex != -1) && (atIndex < colonIndex))
            {
                entity.OClassName = recordString.Substring(0, atIndex);
                index = atIndex + 1;
            }

            // start document parsing with first field name
            IDictionary<string, object> waterBucket = new Dictionary<string, object>();

            do
            {               
                index = ParseFieldName<TResultType>(index, recordString, waterBucket);
            } while (index < recordString.Length);

            // Will probably need a check here to determine how many actual distinct documents we
            // have as this could very well be a collection. Will need a way to determine this here
            // (Normally done while requesting data with PayloadStatus.

            entity.Hydrate(waterBucket);

            return new List<TResultType>() { entity }; // Just for now.
        }

        private int ParseFieldName<TResultType>(int index, string recordString, IDictionary<string, object> waterBucket)
        {
            int startIndex = index;

            int iColonPos = recordString.IndexOf(':', index);
            if (iColonPos == -1)
                return recordString.Length;

            index = iColonPos;

            // parse field name string from raw document string
            string fieldName = recordString.Substring(startIndex, index - startIndex);
            int pos = fieldName.IndexOf('@');
            if (pos > 0)
            {
                fieldName = fieldName.Substring(pos + 1, fieldName.Length - pos - 1);
            }

            fieldName = fieldName.Replace("\"", "");

            waterBucket.Add(fieldName, null);

            // move to position after colon (:)
            index++;

            // check if it's not the end of document which means that current field has null value
            if (index == recordString.Length)
            {
                return index;
            }

            // check what follows after parsed field name and start parsing underlying type
            switch (recordString[index])
            {
                case '"':
                    index = ParseString(index, recordString, waterBucket, fieldName);
                    break;
                case '#':
                    index = ParseRecordID(index, recordString, waterBucket, fieldName);
                    break;
                case '(':
                    index = ParseEmbeddedDocument(index, recordString, waterBucket, fieldName);
                    break;
                case '[':
                    index = ParseList(index, recordString, waterBucket, fieldName);
                    break;
                case '<':
                    index = ParseSet(index, recordString, waterBucket, fieldName);
                    break;
                case '{':
                    index = ParseEmbeddedDocument(index, recordString, waterBucket, fieldName);
                    break;
                case '%':
                    index = ParseRidBags(index, recordString, waterBucket, fieldName);
                    break;
                default:
                    index = ParseValue(index, recordString, waterBucket, fieldName);
                    break;
            }

            // check if it's not the end of document which means that current field has null value
            if (index == recordString.Length)
            {
                return index;
            }

            // single string value was parsed and we need to push the index if next character is comma
            if (recordString[index] == ',')
            {
                index++;
            }

            return index;
        }

        private int ParseValue(int index, string recordString, IDictionary<string, object> waterBucket, string fieldName)
        {
            throw new NotImplementedException();
        }

        private int ParseRidBags(int index, string recordString, IDictionary<string, object> waterBucket, string fieldName)
        {
            throw new NotImplementedException();
        }

        private int ParseSet(int index, string recordString, IDictionary<string, object> waterBucket, string fieldName)
        {
            throw new NotImplementedException();
        }

        private int ParseList(int index, string recordString, IDictionary<string, object> waterBucket, string fieldName)
        {
            throw new NotImplementedException();
        }

        private int ParseEmbeddedDocument(int index, string recordString, IDictionary<string, object> waterBucket, string fieldName)
        {
            throw new NotImplementedException();
        }

        private int ParseRecordID(int index, string recordString, IDictionary<string, object> waterBucket, string fieldName)
        {
            throw new NotImplementedException();
        }

        private int ParseString(int index, string recordString, IDictionary<string, object> waterBucket, string fieldName)
        {
            throw new NotImplementedException();
        }

        public byte[] Serialize<T>(T input) where T : IOrientDBEntity
        {
            return Encoding.UTF8.GetBytes($"{input.OClassName}@{SerializeEntity(input)}");
        }

        private object SerializeEntity(IOrientDBEntity input)
        {
            StringBuilder stringBuilder = new StringBuilder();

            PropertyInfo[] properties = input.GetType().GetProperties();
            
            if(properties.Any())
            {
                foreach(PropertyInfo propertyInfo in properties)
                {
                    if((!string.IsNullOrWhiteSpace(propertyInfo.Name)) && (propertyInfo.Name[0] != '@'))
                    {
                        if (stringBuilder.Length > 0)
                            stringBuilder.Append(",");

                        stringBuilder.AppendFormat("{0}:{1}", propertyInfo.Name, SerializeValue(propertyInfo.GetValue(input), propertyInfo.PropertyType));
                    }
                }
            }

            return stringBuilder.ToString();
        }

        private string SerializeValue(object value, Type valueType)
        {
            if (value == null)
                return string.Empty;

            if (valueType == typeof(byte[]))
            {
                var bytes = value as byte[];
                if (bytes != null)
                {
                    return "_" + Convert.ToBase64String(bytes) + "_";
                }
            }

            switch(TypeExtensionMethods.GetTypeCode(valueType))
            {
                case TypeCode.Empty:
                    break;
                case TypeCode.Boolean:
                    return value.ToString().ToLower();
                case TypeCode.Byte:
                    return value.ToString() + "b";
                case TypeCode.Int16:
                    return value.ToString() + "s";
                case TypeCode.Int32:
                    return value.ToString();
                case TypeCode.Int64:
                    return value.ToString() + "l";
                case TypeCode.Single:
                    return ((float)value).ToString(CultureInfo.InvariantCulture) + "f";
                case TypeCode.Double:
                    return ((double)value).ToString(CultureInfo.InvariantCulture) + "d";
                case TypeCode.Decimal:
                    return ((decimal)value).ToString(CultureInfo.InvariantCulture) + "c";
                case TypeCode.DateTime:
                    DateTime unixEpoch = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);
                    return ((long)((DateTime)value - unixEpoch).TotalMilliseconds).ToString() + "t";
                case TypeCode.String:
                case TypeCode.Char:
                    // strings must escape these characters:
                    // " -> \"
                    // \ -> \\
                    string stringValue = value.ToString();
                    // escape quotes
                    stringValue = stringValue.Replace("\\", "\\\\");
                    // escape backslashes
                    stringValue = stringValue.Replace("\"", "\\" + "\"");

                    return "\"" + stringValue + "\"";
                case TypeCode.Object:
                    return SerializeObjectValue(value, valueType);
            }
            throw new NotImplementedException();
        }

        private string SerializeObjectValue(object value, Type valueType)
        {
            StringBuilder bld = new StringBuilder();

            if ((valueType.IsArray) || (valueType.GetTypeInfo().IsGenericType))
            {
                if (valueType.Name == "Dictionary`2")
                {
                    bld.Append("{");

                    IDictionary<string, object> collection = (IDictionary<string, object>)value;

                    bool first = true;
                    foreach (var keyVal in collection)
                    {
                        if (!first)
                            bld.Append(",");

                        first = false;

                        string serialized = SerializeValue(keyVal.Value);
                        bld.Append("\"" + keyVal.Key + "\":" + serialized);
                    }

                    bld.Append("}");
                }
                else
                {
                    bld.Append(valueType.Name == "HashSet`1" ? "<" : "[");

                    IEnumerable collection = (IEnumerable)value;

                    bool first = true;
                    foreach (object val in collection)
                    {
                        if (!first)
                            bld.Append(",");

                        first = false;
                        bld.Append(SerializeValue(val));
                    }

                    bld.Append(valueType.Name == "HashSet`1" ? ">" : "]");
                }
            }
            // if property is ORID type it needs to be serialized as ORID
            else if (valueType.GetTypeInfo().IsClass && (valueType.Name == "ORID"))
            {
                bld.Append(((ORID)value).RID);
            }
            // Not sure this is possible with this architecture.
            //else if (valueType.GetTypeInfo().IsClass && (valueType.Name == "ODocument"))
            //{
            //    bld.AppendFormat("({0})", SerializeDocument((ODocument)value));
            //}
            // Not sure on this one either, testing will tell.
            else if (valueType.GetTypeInfo().IsClass && (valueType.Name == "OEmbeddedRidBag"))
            {
                //bld.AppendFormat("({0})", SerializeDocument((ODocument)value));
                List<ORID> ridbag = (List<ORID>)value;
                if (ridbag.Count > 0)
                {
                    BinaryBuffer buffer = new BinaryBuffer();
                    bld.Append("%");
                    buffer.Write((byte)1); // config
                    buffer.Write(ridbag.Count); //size
                    foreach (var item in ridbag)
                    {
                        buffer.Write(item);
                    }
                    bld.Append(Convert.ToBase64String(buffer.ToArray()));
                    bld.Append(";");
                }
            }

            return bld.ToString();
        }

        private string SerializeValue(object val)
        {
            return SerializeValue(val, val.GetType());
        }
    }
}
