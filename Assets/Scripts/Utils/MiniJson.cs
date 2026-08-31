using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;

public static class MiniJson
{
    public static string Serialize(object obj)
    {
        if (obj == null) return "null";
        if (obj is string) return "\"" + ((string)obj).Replace("\"", "\\\"") + "\"";
        if (obj is bool) return (bool)obj ? "true" : "false";
        if (obj is IDictionary dict)
        {
            var items = new List<string>();
            foreach (var key in dict.Keys)
                items.Add(Serialize(key.ToString()) + ":" + Serialize(dict[key]));
            return "{" + string.Join(",", items) + "}";
        }
        if (obj is IEnumerable enumerable && !(obj is string))
        {
            var items = new List<string>();
            foreach (var element in enumerable)
                items.Add(Serialize(element));
            return "[" + string.Join(",", items) + "]";
        }
        if (obj is float || obj is double || obj is int || obj is long)
            return Convert.ToString(obj, System.Globalization.CultureInfo.InvariantCulture);

        return "\"" + obj.ToString() + "\"";
    }
}
