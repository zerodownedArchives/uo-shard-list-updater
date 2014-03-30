using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using MySql.Data.MySqlClient;
using System.Net.Sockets;
using System.Net;
using System.Text.RegularExpressions;
using System.IO;
using System.Diagnostics;

namespace UOShardListUpdater
{
    class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("Welcome to UO Shard List Updater by Felladrin.");

            const int minutesToWait = 5;

            while (true)
            {
                Stopwatch sw = new Stopwatch();

                sw.Start();
                
                string cs = @"server=YOUR_MYSQL_HOST;userid=YOUR_MYSQL_USER;password=YOUR_MYSQL_PASSWORD;database=YOUR_MYSQL_DATABASE";

                Console.WriteLine();
                Console.WriteLine(":: Started checking at {0} ::", DateTime.Now);
                Console.WriteLine();

                MySqlConnection conn = null;
                MySqlDataReader rdr = null;

                try
                {
                    File.Delete(Directory.GetCurrentDirectory() + "\\returnlog.txt");
                    
                    conn = new MySqlConnection(cs);
                    conn.Open();

                    string stm = "SELECT id, name, host, port, online_last_time, online_min, online_max, send_packets FROM shard ORDER BY online_now DESC, online DESC";
                    MySqlCommand cmd = new MySqlCommand(stm, conn);
                    rdr = cmd.ExecuteReader();

                    MySqlConnection con2 = new MySqlConnection(cs);
                    con2.Open();
                    MySqlCommand cmd2 = null;

                    while (rdr.Read())
                    {
                        int id = rdr.GetInt32(0);
                        string name = rdr.GetString(1);
                        string host = rdr.GetString(2);
                        string port = rdr.GetString(3);
                        DateTime online_last_time = (rdr.IsDBNull(4)) ? DateTime.Now : rdr.GetDateTime(4);
                        int online_min = (rdr.IsDBNull(5)) ? -1 : rdr.GetInt32(5);
                        int online_max = (rdr.IsDBNull(6)) ? -1 : rdr.GetInt32(6);
                        bool send_packets = rdr.GetBoolean(7);
                        int onlineNow = getClientsOnline(host, port, send_packets);

                        if (send_packets)
                        {
                            Console.WriteLine("{0} in {1}", String.Format("{0,4}", onlineNow), name);
                        }
                        else
                        {
                            Console.WriteLine("{0} in {1} (No packets sent)", String.Format("{0,4}", onlineNow), name);
                        }

                        if (onlineNow == -1)
                        {
                            string cmdUpdate = string.Format("UPDATE shard SET online=FALSE, online_now=0 WHERE id='{0}'", id);
                            cmd2 = new MySqlCommand(cmdUpdate, con2);
                            cmd2.ExecuteNonQuery();
                        }
                        else if (onlineNow == 0)
                        {
                            string cmdUpdate = string.Format("UPDATE shard SET online=TRUE, online_now=0, online_last_time='{0}' WHERE id='{1}'", (DateTime.Now).ToString("yyyy-MM-dd HH:mm:ss"), id);
                            cmd2 = new MySqlCommand(cmdUpdate, con2);
                            cmd2.ExecuteNonQuery();
                        }
                        else
                        {
                            if (online_min == -1)
                            {
                                string cmdUpdate = string.Format("UPDATE shard SET online=TRUE, online_last_time='{0}', online_now='{1}', online_min='{1}', online_max='{1}' WHERE id='{2}'", (DateTime.Now).ToString("yyyy-MM-dd HH:mm:ss"), onlineNow, id);
                                cmd2 = new MySqlCommand(cmdUpdate, con2);
                                cmd2.ExecuteNonQuery();
                            }
                            else if (onlineNow < online_min)
                            {
                                string cmdUpdate = string.Format("UPDATE shard SET online=TRUE, online_last_time='{0}', online_now='{1}', online_min='{1}' WHERE id='{2}'", (DateTime.Now).ToString("yyyy-MM-dd HH:mm:ss"), onlineNow, id);
                                cmd2 = new MySqlCommand(cmdUpdate, con2);
                                cmd2.ExecuteNonQuery();
                            }
                            else if (onlineNow > online_max)
                            {
                                string cmdUpdate = string.Format("UPDATE shard SET online=TRUE, online_last_time='{0}', online_now='{1}', online_max='{1}' WHERE id='{2}'", (DateTime.Now).ToString("yyyy-MM-dd HH:mm:ss"), onlineNow, id);
                                cmd2 = new MySqlCommand(cmdUpdate, con2);
                                cmd2.ExecuteNonQuery();
                            }
                            else
                            {
                                string cmdUpdate = string.Format("UPDATE shard SET online=TRUE, online_last_time='{0}', online_now='{1}' WHERE id='{2}'", (DateTime.Now).ToString("yyyy-MM-dd HH:mm:ss"), onlineNow, id);
                                cmd2 = new MySqlCommand(cmdUpdate, con2);
                                cmd2.ExecuteNonQuery();
                            }
                        }
                    }

                    string cmdDelete = "DELETE FROM shard WHERE online_last_time < DATE_SUB( CURDATE() , INTERVAL 7 DAY )";
                    cmd2 = new MySqlCommand(cmdDelete, con2);
                    cmd2.ExecuteNonQuery();

                    con2.Close();

                    Console.WriteLine();
                    Console.WriteLine(":: Finished checking at {0} ::", DateTime.Now);
                    Console.WriteLine();
                }
                catch (Exception e)
                {
                    log(e);
                }
                finally
                {
                    if (rdr != null)
                    {
                        rdr.Close();
                    }

                    if (conn != null)
                    {
                        conn.Close();
                    }
                }

                sw.Stop();

                int sleepTime = (minutesToWait * 60 * 1000) - Convert.ToInt32(sw.Elapsed.TotalMilliseconds);

                if (sleepTime > 0)
                {
                    Console.WriteLine("Next update in " + (sleepTime / 1000) + " seconds.");

                    System.Threading.Thread.Sleep(sleepTime);
                }
            }
        }

        public static int getClientsOnline(string host, string port, bool send_packets)
        {
            TcpClient client = new TcpClient();

            IPAddress ip;

            int portInt;

            IPEndPoint serverEndPoint;

            NetworkStream clientStream;

            Match match;

            try
            {
                ip = Dns.GetHostAddresses(host)[0];
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message.ToString());
                return -1;
            }

            try
            {
                portInt = Convert.ToInt32(port);
            }
            catch (Exception e)
            {
                log(e);
                return -1;
            }

            try
            {
                serverEndPoint = new IPEndPoint(ip, portInt);
            }
            catch (Exception e)
            {
                log(e);
                return -1;
            }

            try
            {
                client.Connect(serverEndPoint);
            }
            catch
            {
                client.Close();
                return -1;
            }

            if (!send_packets)
            {
                client.Close();
                return 0;
            }

            try
            {
                clientStream = client.GetStream();
            }
            catch (Exception e)
            {
                log(e);
                return 0;
            }

            clientStream.ReadTimeout = 2000;

            clientStream.WriteTimeout = 2000;

            byte[] buffer = new byte[] { 127, 0, 0, 1, 241, 0, 4, 255 };

            try
            {
                clientStream.Write(buffer, 0, buffer.Length);
            }
            catch (Exception e)
            {
                log(e);
                return 0;
            }

            clientStream.Flush();

            byte[] message = new byte[128];

            int bytesRead = 0;

            try
            {
                bytesRead = clientStream.Read(message, 0, 128);
            }
            catch
            {
                clientStream.Close();
                client.Close();
                return 0;
            }

            if (bytesRead == 0)
            {
                clientStream.Close();
                client.Close();
                return 0;
            }

            int clientsOnline = 0;

            string msgReturned;

            try
            {
                msgReturned = Encoding.UTF8.GetString(message, 0, bytesRead);
                File.AppendAllText(Directory.GetCurrentDirectory() + "\\returnlog.txt", host + ":" + port + " (" + DateTime.Now + ")" + Environment.NewLine + msgReturned + Environment.NewLine + Environment.NewLine);
            }
            catch (Exception e)
            {
                log(e);
                clientStream.Close();
                client.Close();
                return 0;
            }

            try
            {
                match = Regex.Match(msgReturned, @"Clients=([0-9]{1,})");
            }
            catch (Exception e)
            {
                log(e);
                clientStream.Close();
                client.Close();
                return 0;
            }

            if (match.Success)
            {
                string value = match.Groups[1].Value;

                try
                {
                    clientsOnline = Convert.ToInt32(match.Groups[1].Value) - 1;
                }
                catch (Exception e)
                {
                    log(e);
                    clientStream.Close();
                    client.Close();
                    return 0;
                }
            }

            clientStream.Close();

            client.Close();

            return clientsOnline;
        }

        public static void log(Exception exception)
        {
            try
            {
                if (!string.IsNullOrEmpty(exception.ToString()))
                {
                    string path = Directory.GetCurrentDirectory() + "\\errorlog.txt";
                    string toWrite = DateTime.Now + Environment.NewLine + exception.ToString() + Environment.NewLine + Environment.NewLine;
                    File.AppendAllText(path, toWrite);
                    Console.WriteLine("An error occurred at {0}. Check the error log.", System.DateTime.Now);
                }
            }
            catch (Exception e)
            {
                Console.WriteLine("The process failed: {0}", e.ToString());
            }
        }
    }
}
