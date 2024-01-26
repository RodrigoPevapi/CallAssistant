using System.Diagnostics;
using System.Net.NetworkInformation;
using System.Data.SqlClient;
using System.Text.RegularExpressions;

class Program
{
    private static bool _continuePing = true;

    static void Main(string[] args)
    {
        do
        {
            Console.Clear();
            Console.Write("Search Unit: ");
            string input = Console.ReadLine();
            string whereClause = string.Empty;

            if (Regex.IsMatch(input, @"^\d{4}$"))
            {
                Console.WriteLine("Searching by last 4 digits of Serial Number.");
                whereClause = "RIGHT(u.[serial_number], 4) = @input";
            }
            else if (Regex.IsMatch(input, @"\b\d{1,3}(\.\d{1,3}){3}\b"))
            {
                Console.WriteLine("Searching by IP Address.");
                whereClause = "sc.[IP] = @input";
            }
            else
            {
                Console.WriteLine("Searching by Serial Number.");
                input = "%" + input + "%"; 
                whereClause = "u.[serial_number] LIKE @input"; 
            }

            string query = $@"
                SELECT
                sc.[IP] AS SimCardIP,
                sc.[nvr_id],
                sc.[unit_id],
                n.[IP] AS NvrIP,
                n.[name] AS NvrName
                FROM 
                    [dbo].[sim_cards] sc
                INNER JOIN 
                    [dbo].[units] u ON sc.[unit_id] = u.[id]
                INNER JOIN 
                    [dbo].[nvrs] n ON sc.[nvr_id] = n.[id]
                WHERE 
                    {whereClause}";

            ExecuteQuery(query, input);

            Console.WriteLine("Restarting the program...");
            Thread.Sleep(3000);
        } while (true);
    }

    static void ExecuteQuery(string query, string parameter)
    {
        string connectionString = "Server=liveguardtech.database.windows.net;Database=cammaster_prod;User Id=Thor;Password=LGsam@2021;MultipleActiveResultSets=true;Encrypt=false;";
        try
        {
            using SqlConnection connection = new SqlConnection(connectionString);
            connection.Open();

            SqlCommand command = new SqlCommand(query, connection);
            command.Parameters.AddWithValue("@input", parameter);

            using (SqlDataReader reader = command.ExecuteReader())
            {
                while (reader.Read())
                {
                    string simCardIP = reader["SimCardIP"].ToString();
                    string nvrIP = reader["NvrIP"].ToString();
                    string unit_id = reader["unit_id"].ToString();

                    OpenWebPages(simCardIP, nvrIP, unit_id);

                    Thread pingThread = new Thread(() => ContinuousPing(simCardIP));
                    pingThread.Start();

                    Console.WriteLine("Press any key to stop the ping...");
                    Console.ReadKey();

                    _continuePing = false;
                    pingThread.Join();

                    Console.WriteLine("Ping stopped.");
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine("Error: " + ex.Message);
        }
    }

    static void OpenWebPages(string simCardIP, string nvrIP, string unit_id)
    {
        string[] urls = {
        $"https://cammaster.liveguardtech.com/Units/Details/{unit_id}",
        $"http://{simCardIP}:81",
        $"http://{simCardIP}:82",
        $"http://{simCardIP}:83",
        $"http://{simCardIP}:85",
        $"http://{simCardIP}",
        $"http://{nvrIP}"
    };

        foreach (string url in urls)
        {
            try
            {
                Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
                Thread.Sleep(1000); 
            }
            catch (Exception ex)
            {
                Console.WriteLine($"No se pudo abrir la URL {url}: {ex.Message}");
            }
        }
    }

    static void ContinuousPing(string ipAddress)
    {
        using (Ping ping = new Ping())
        {
            _continuePing = true; 
            while (_continuePing)
            {
                try
                {
                    PingReply reply = ping.Send(ipAddress);
                    if (reply.Status == IPStatus.Success)
                    {
                        Console.WriteLine($"Ping to {ipAddress}: bytes={reply.Buffer.Length} time={reply.RoundtripTime}ms TTL={reply.Options.Ttl}");
                    }
                    else
                    {
                        Console.WriteLine($"Ping to {ipAddress}: {reply.Status}");
                    }
                    Thread.Sleep(1000);
                }
                catch (Exception ex)
                {
                    Console.WriteLine("Error during ping: " + ex.Message);
                }
            }
        }
    }
}
