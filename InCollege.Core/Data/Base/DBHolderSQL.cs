﻿using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SQLite;
using System.Linq;

namespace InCollege.Core.Data.Base
{
    /// <summary>
    /// DO NOT USE ON CLIENT SIDE!!!11
    /// </summary>
    public static class DBHolderSQL
    {
        static SQLiteConnection DataConnection;
        static Dictionary<string, SQLiteDataAdapter> Adapters;
        public static void Init(string filename)
        {
            //Create required tables and... at all, prepare db.
            //I hope this isn't shit-code-design.
            DBHolderORM.Init(filename);

            DataConnection = new SQLiteConnection(string.Format("Data Source={0}; Version=3;", filename));
            DataConnection.Open();
            Adapters = new Dictionary<string, SQLiteDataAdapter>();

            foreach (DataRow current in DataConnection.GetSchema("Tables").Rows)
                Adapters.Add(current[2].ToString(), new SQLiteDataAdapter("SELECT * FROM {0} LIMIT {1}, {2}", DataConnection));
        }

        public static DataSet GetRange(string table, int skip, int count, params Tuple<string, object>[] whereParams)
        {
            if (count == -1)
                count = DBHolderORM.DEFAULT_LIMIT;
            if (table == null)
                table = "master_table";
            DataSet result = new DataSet(table);
            string whereString = null;
            if (whereParams != null)
            {
                whereString = "";
                for (int i = 0; i < whereParams.Length; i++)
                {
                    whereString += string.Format(whereParams[i].Item2 is string?"instr({0}, '{1}') > 0":"{0} LIKE {1}", whereParams[i].Item1, whereParams[i].Item2);
                    if (i < whereParams.Length - 1)
                        whereString += " AND ";
                }
            }
            string debug = string.Format("SELECT * FROM {0} " +
                (string.IsNullOrWhiteSpace(whereString) ? "" : "WHERE {3} ") +
                                        "LIMIT {1}, {2} ",
                                        table, skip, count, whereString);
            Adapters[table].SelectCommand =
                new SQLiteCommand(
                    string.Format("SELECT * FROM {0} " +
                                  (string.IsNullOrWhiteSpace(whereString) ? "" : "WHERE {3} ") +
                                  "LIMIT {1}, {2} ",
                                  table, skip, count, whereString),
                    DataConnection);
            Adapters[table].Fill(result);
            return result;
        }

        public static DataRow GetByID(string table, int id)
        {
            DataTable result = new DataTable();
            Adapters[table].SelectCommand = new SQLiteCommand(string.Format("SELECT * FROM {0} WHERE ID={1}", table, id), DataConnection);
            Adapters[table].Fill(result);
            return result.Rows.Count > 0 ? result.Rows[0] : null;
        }

        public static int Save(string table, params Tuple<string, object>[] columns)
        {
            var adapter = Adapters[table];
            var data = new DataTable(table);
            data.PrimaryKey = new[] { data.Columns["ID"] };
            adapter.Fill(data);

            DataRow row;
            int id;
            bool isLocal;
            if (!(isLocal = (bool)columns.First(c => c.Item1.Equals("IsLocal")).Item2) &&
                (id = (int)columns.First(c => c.Item1.Equals("ID")).Item2) > 0 &&
                data.Rows.Contains(id))

                row = data.Rows.Find(id);
            else
                row = data.NewRow();
            foreach (var current in columns)
                if ((current.Item1 != "ID" || isLocal) && current.Item1 != "IsLocal")
                    row[current.Item1] = current.Item2;
            adapter.InsertCommand = new SQLiteCommandBuilder(adapter).GetInsertCommand(true);
            adapter.UpdateCommand = new SQLiteCommandBuilder(adapter).GetUpdateCommand(true);
            return adapter.Update(data);
        }

        public static int Remove(string table, int id)
        {
            return new SQLiteCommand(string.Format("DELETE FROM {0} WHERE ID={1}"), DataConnection).ExecuteNonQuery();
        }

        static int GetFreeID(string table)
        {
            Adapters[table].SelectCommand = new SQLiteCommand(string.Format("SELECT ID FROM {0}", table), DataConnection);
            DataTable data = new DataTable();
            Adapters[table].Fill(data);
            int result = 0;
            foreach (DataRow current in data.Rows)
                if ((int)current["ID"] > result) result = (int)current["ID"];
            return result;
        }
    }
}
