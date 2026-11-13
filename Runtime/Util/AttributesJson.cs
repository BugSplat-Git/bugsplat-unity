using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace BugSplatUnity.Runtime.Util
{
    internal static class AttributesJson
    {
        public static string Serialize(IDictionary<string, string> attributes)
        {
            var json = new StringBuilder("{");

            if (attributes != null)
            {
                var first = true;
                foreach (var attribute in attributes)
                {
                    if (!first)
                    {
                        json.Append(',');
                    }
                    first = false;

                    AppendString(json, attribute.Key);
                    json.Append(':');
                    if (attribute.Value == null)
                    {
                        json.Append("null");
                    }
                    else
                    {
                        AppendString(json, attribute.Value);
                    }
                }
            }

            return json.Append('}').ToString();
        }

        private static void AppendString(StringBuilder json, string value)
        {
            json.Append('"');
            foreach (var c in value)
            {
                switch (c)
                {
                    case '"': json.Append("\\\""); break;
                    case '\\': json.Append("\\\\"); break;
                    case '\b': json.Append("\\b"); break;
                    case '\f': json.Append("\\f"); break;
                    case '\n': json.Append("\\n"); break;
                    case '\r': json.Append("\\r"); break;
                    case '\t': json.Append("\\t"); break;
                    default:
                        if (c < 0x20 || c > 0x7E)
                        {
                            json.Append("\\u").Append(((int)c).ToString("x4", CultureInfo.InvariantCulture));
                        }
                        else
                        {
                            json.Append(c);
                        }
                        break;
                }
            }
            json.Append('"');
        }
    }
}
