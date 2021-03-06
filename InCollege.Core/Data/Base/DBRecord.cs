﻿using Newtonsoft.Json;
using SQLite;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Reflection;

namespace InCollege.Core.Data.Base
{
    public abstract class DBRecord
    {
        [PrimaryKey]
        [AutoIncrement]
        public int ID { get; set; } = -1;
        // [NotNull]
        public bool IsLocal { get; set; } = true;
        // [NotNull]
        public bool Modified { get; set; } = true;
        // [NotNull]
        public bool Removed { get; set; } = false;

        [Ignore]
        [JsonIgnore]
        public string StateIconURI
        {
            get
            {
                return Removed ? "RemovedIcon" : IsLocal || Modified ? "ModifiedIcon" : "OkIcon";
            }
        }

        public string POSTSerialized
        {
            get => $"table={GetType().Name}&" + string.Join("&", Columns.Select(c => $"field{c.name}={WebUtility.UrlEncode(c.value.ToString())}"));
        }

        public string POSTSerializedClear
        {
            get => $"table={GetType().Name}&" + string.Join("&", Columns.Select(c => $"{c.name}={WebUtility.UrlEncode(c.value.ToString())}"));
        }

        public IEnumerable<(string name, object value)> Columns
        {
            get => GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance)
                            .Where(c =>
                            {
                                if (c.CanWrite && !Attribute.IsDefined(c, typeof(IgnoreAttribute)))
                                {
                                    object value = c.GetValue(this);
                                    return value != null && (!(value is string) || !string.IsNullOrWhiteSpace((string)value));
                                }
                                return false;
                            })
                            .Select(c =>
                            {
                                var value = c.GetValue(this);
                                return (c.Name, ((value is byte[]) ? Convert.ToBase64String((byte[])value) :
                                                       (value is bool) ? ((bool)value ? 1 : 0) :
                                                       (value is Enum) ? (byte)value :
                                                       (value is DateTime) ? ((DateTime)value).ToString(CommonVariables.DateFormatString) :
                                                       value));
                            });
        }
    }
}
