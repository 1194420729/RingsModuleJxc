using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Npgsql;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;

namespace JxcTesting
{
    class DBHelper:IDisposable
    {
        
        private string connectionstring = null;

        public DBHelper()
        {
            this.connectionstring = "Server=127.0.0.1;Port=5432;uid=postgres;pwd=Password01!;Database=ringstesting;Encoding=UNICODE";
        }

        public List<TableModel> Query(string tablename, string condition, string orderby, out int recordcount)
        {
            DataTable dt = new DataTable();

            if (this.connection != null)
            {
                NpgsqlCommand command = new NpgsqlCommand();
                command.Connection = this.connection;
                command.Transaction = this.transaction;

                command.CommandText = "select count(0) as cnt from \"" + tablename + "\" where 1=1 "
                        + condition;
                recordcount = Convert.ToInt32(command.ExecuteScalar());

                command.CommandText = "select id,content from \"" + tablename + "\" where 1=1 "
                    + condition + orderby;
                NpgsqlDataAdapter da = new NpgsqlDataAdapter(command);
                da.Fill(dt);

            }
            else
            {
                using (NpgsqlConnection connection = new NpgsqlConnection(this.connectionstring))
                {
                    connection.Open();
                    NpgsqlCommand command = new NpgsqlCommand();
                    command.Connection = connection;

                    command.CommandText = "select count(0) as cnt from \"" + tablename + "\" where 1=1 "
                        + condition;
                    recordcount = Convert.ToInt32(command.ExecuteScalar());

                    command.CommandText = "select id,content from \"" + tablename + "\" where 1=1 "
                        + condition + orderby;
                    NpgsqlDataAdapter da = new NpgsqlDataAdapter(command);
                    da.Fill(dt);

                    connection.Close();
                }
            }

            List<TableModel> list = new List<TableModel>();
            foreach (DataRow row in dt.Rows)
            {
                list.Add(new TableModel()
                {
                    id = Convert.ToInt32(row["id"]),
                    content = JsonConvert.DeserializeObject<JObject>(row["content"].ToString())
                });
            }

            return list;
        }


        public List<TableModel> Where(string sql)
        {
            DataTable dt = new DataTable();

            if (this.connection != null)
            {
                NpgsqlCommand command = new NpgsqlCommand();
                command.Connection = this.connection;
                command.Transaction = this.transaction;
                command.CommandText = sql;

                NpgsqlDataAdapter da = new NpgsqlDataAdapter(command);
                da.Fill(dt);

            }
            else
            {
                using (NpgsqlConnection connection = new NpgsqlConnection(this.connectionstring))
                {
                    connection.Open();
                    NpgsqlCommand command = new NpgsqlCommand();
                    command.Connection = connection;

                    command.CommandText = sql;

                    NpgsqlDataAdapter da = new NpgsqlDataAdapter(command);
                    da.Fill(dt);

                    connection.Close();
                }
            }
            List<TableModel> list = new List<TableModel>();
            foreach (DataRow row in dt.Rows)
            {
                list.Add(new TableModel()
                {
                    id = Convert.ToInt32(row["id"]),
                    content = JsonConvert.DeserializeObject<JObject>(row["content"].ToString())
                });
            }

            return list;
        }

        public DataTable QueryTable(string sql)
        {
            DataTable dt = new DataTable();

            if (this.connection != null)
            {
                NpgsqlCommand command = new NpgsqlCommand();
                command.Connection = this.connection;
                command.Transaction = this.transaction;
                command.CommandText = sql;

                NpgsqlDataAdapter da = new NpgsqlDataAdapter(command);
                da.Fill(dt);

            }
            else
            {
                using (NpgsqlConnection connection = new NpgsqlConnection(this.connectionstring))
                {
                    connection.Open();
                    NpgsqlCommand command = new NpgsqlCommand();
                    command.Connection = connection;

                    command.CommandText = sql;

                    NpgsqlDataAdapter da = new NpgsqlDataAdapter(command);
                    da.Fill(dt);

                    connection.Close();
                }
            }

            return dt;
        }

        public TableModel First(string sql)
        {
            if (sql.EndsWith("limit 1") == false)
            {
                sql = sql + " limit 1";
            }
            List<TableModel> list = Where(sql);

            return list.First();
        }

        public TableModel First(string tablename, int id)
        {
            List<TableModel> list = Where("select * from \"" + tablename + "\" where id=" + id);

            return list.First();
        }

        public TableModel FirstOrDefault(string sql)
        {
            if (sql.EndsWith("limit 1") == false)
            {
                sql = sql + " limit 1";
            }
            List<TableModel> list = Where(sql);

            return list.FirstOrDefault();
        }

        public object Scalar(string sql)
        {
            object obj = null;

            if (this.connection != null)
            {
                NpgsqlCommand command = new NpgsqlCommand();
                command.Connection = this.connection;
                command.Transaction = this.transaction;

                command.CommandText = sql;

                obj = command.ExecuteScalar();

            }
            else
            {
                using (NpgsqlConnection connection = new NpgsqlConnection(this.connectionstring))
                {
                    connection.Open();
                    NpgsqlCommand command = new NpgsqlCommand();
                    command.Connection = connection;

                    command.CommandText = sql;

                    obj = command.ExecuteScalar();

                    connection.Close();
                }
            }

            return obj;
        }

        public int Count(string sql)
        {
            return Convert.ToInt32(Scalar(sql));
        }

        private NpgsqlConnection connection = null;
        private NpgsqlTransaction transaction = null;

        private void InitConnection()
        {
            this.connection = new NpgsqlConnection(this.connectionstring);
            this.connection.Open();
            this.transaction = this.connection.BeginTransaction();

        }

        public void Edit(string tablename, TableModel model)
        {
            if (this.connection == null)
            {
                InitConnection();
            }

            try
            {
                NpgsqlCommand command = new NpgsqlCommand();
                command.Connection = this.connection;
                command.Transaction = this.transaction;
                command.CommandText = "update \"" + tablename + "\" set content=@content where id=" + model.id;
                command.Parameters.Add("@content", NpgsqlTypes.NpgsqlDbType.Jsonb).Value = JsonConvert.SerializeObject(model.content);
                command.ExecuteNonQuery();
            }
            catch (Exception ex)
            {
                transaction.Rollback();
                this.connection.Close();
                this.connection = null;
                this.transaction = null;

                throw ex;
            }
        }

        public void Add(string tablename, TableModel model)
        {
            if (this.connection == null)
            {
                InitConnection();
            }

            try
            {
                NpgsqlCommand command = new NpgsqlCommand();
                command.Connection = this.connection;
                command.Transaction = this.transaction;
                command.CommandText = "insert into \"" + tablename + "\" (content) values (@content) returning id";
                command.Parameters.Add("@content", NpgsqlTypes.NpgsqlDbType.Jsonb).Value = JsonConvert.SerializeObject(model.content);
                model.id = Convert.ToInt32(command.ExecuteScalar());
            }
            catch (Exception ex)
            {
                transaction.Rollback();
                this.connection.Close();
                this.connection = null;
                this.transaction = null;

                throw ex;
            }
        }

        public void AddRange(string tablename, ICollection<TableModel> models)
        {
            foreach (var item in models)
            {
                this.Add(tablename, item);
            }
        }

        public void Remove(string tablename, int id)
        {
            if (this.connection == null)
            {
                InitConnection();
            }

            try
            {
                NpgsqlCommand command = new NpgsqlCommand();
                command.Connection = this.connection;
                command.Transaction = this.transaction;
                command.CommandText = "delete from \"" + tablename + "\" where id=" + id;
                command.ExecuteNonQuery();
            }
            catch (Exception ex)
            {
                transaction.Rollback();
                this.connection.Close();
                this.connection = null;
                this.transaction = null;

                throw ex;
            }
        }

        public void Remove(string tablename, TableModel model)
        {
            Remove(tablename, model.id);
        }

        public void RemoveRange(string tablename, ICollection<TableModel> models)
        {
            foreach (var item in models)
            {
                this.Remove(tablename, item);
            }
        }

        public void RemoveRange(string tablename, string ids, char separator = ',')
        {
            string[] ss = ids.Split(new char[] { separator }, StringSplitOptions.RemoveEmptyEntries);
            foreach (string s in ss)
            {
                this.Remove(tablename, Convert.ToInt32(s));
            }
        }

        public void Truncate(string tablename)
        {
            if (this.connection == null)
            {
                InitConnection();
            }

            try
            {
                NpgsqlCommand command = new NpgsqlCommand();
                command.Connection = this.connection;
                command.Transaction = this.transaction;
                command.CommandText = "truncate table \"" + tablename + "\"";
                command.ExecuteNonQuery();
            }
            catch (Exception ex)
            {
                transaction.Rollback();
                this.connection.Close();
                this.connection = null;
                this.transaction = null;

                throw ex;
            }
        }

        public void ExcuteNoneQuery(string sql)
        {
            if (this.connection == null)
            {
                InitConnection();
            }

            try
            {
                NpgsqlCommand command = new NpgsqlCommand();
                command.Connection = this.connection;
                command.Transaction = this.transaction;
                command.CommandText = sql;
                command.ExecuteNonQuery();
            }
            catch (Exception ex)
            {
                transaction.Rollback();
                this.connection.Close();
                this.connection = null;
                this.transaction = null;

                throw ex;
            }
        }

        public void SaveChanges()
        {
            if (this.connection == null) return;

            transaction.Commit();
            this.connection.Close();
            this.connection = null;
            this.transaction = null;
        }

        public void Discard()
        {
            if (this.connection == null) return;

            transaction.Rollback();
            this.connection.Close();
            this.connection = null;
            this.transaction = null;
        }

        public void Dispose()
        {
            Discard();
        }
    }

    public class TableModel
    {
        public int id { get; set; }
        public JObject content { get; set; }
    }
}
