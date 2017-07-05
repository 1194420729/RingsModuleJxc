using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Npgsql;
using Rings.Models;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Linq;
using System.Text;

namespace Jxc.Utility
{
    public class ZtHelper
    {
        private string centralconnectionstr = null;

        public ZtHelper()
        {
            this.centralconnectionstr = ConfigurationManager.ConnectionStrings["CentralDB"].ConnectionString;
            
        }

        public TableModel GetRootCorporation()
        {
            DataTable dt = new DataTable();
            using (NpgsqlConnection connection = new NpgsqlConnection(centralconnectionstr))
            {
                connection.Open();
                NpgsqlCommand command = new NpgsqlCommand();
                command.Connection = connection;

                command.CommandText = "select * from corporation where applicationid='" + PluginContext.Current.Account.ApplicationId + "'";

                NpgsqlDataAdapter da = new NpgsqlDataAdapter(command);
                da.Fill(dt);

                connection.Close();
            }

            JObject content = JsonConvert.DeserializeObject<JObject>(dt.Rows[0]["content"].ToString());
            if (content.Value<bool>("default"))
            {
                content.Add("applicationid", dt.Rows[0]["applicationid"].ToString());
                content.Add("corporationname", dt.Rows[0]["name"].ToString());
                content.Add("connectionstring", dt.Rows[0]["connectionstring"].ToString());
                return new TableModel() { id = Convert.ToInt32(dt.Rows[0]["id"]), content = content };
            }
            else
            {
                int rootid = content.Value<int>("rootid");
                return GetRootCorporation(rootid);
            }

        }

        private TableModel GetRootCorporation(int rootid)
        {
            DataTable dt = new DataTable();
            using (NpgsqlConnection connection = new NpgsqlConnection(centralconnectionstr))
            {
                connection.Open();
                NpgsqlCommand command = new NpgsqlCommand();
                command.Connection = connection;

                command.CommandText = "select * from corporation where id=" + rootid;

                NpgsqlDataAdapter da = new NpgsqlDataAdapter(command);
                da.Fill(dt);

                connection.Close();
            }

            JObject content = JsonConvert.DeserializeObject<JObject>(dt.Rows[0]["content"].ToString());
            content.Add("applicationid", dt.Rows[0]["applicationid"].ToString());
            content.Add("corporationname", dt.Rows[0]["name"].ToString());
            content.Add("connectionstring", dt.Rows[0]["connectionstring"].ToString());
            return new TableModel() { id = Convert.ToInt32(dt.Rows[0]["id"]), content = content };
        }

        public bool ZtNameExist(string ztname)
        { 
            var root = this.GetRootCorporation();
            string name = root.content.Value<string>("corporationname") + "_" + ztname;
            int cnt = 0;
            using (NpgsqlConnection connection = new NpgsqlConnection(centralconnectionstr))
            {
                connection.Open();

                NpgsqlCommand command = new NpgsqlCommand();
                command.Connection = connection;

                command.CommandText = "select count(*) as cnt from corporation where name=@name";
                command.Parameters.Add("name", NpgsqlTypes.NpgsqlDbType.Text).Value = name;
                cnt = Convert.ToInt32(command.ExecuteScalar());
                connection.Close();
            }

            return cnt > 0;
        }
    }
}
