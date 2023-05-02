using System;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Weather_Application
{
    public class StationData
    {
        public Station[] station { get; set; }
    }

    public class Station
    {
        public Value[] value { get; set; }
        public string name { get; set; }
    }

    public class Value
    {
        public long from { get; set; }
        public long to { get; set; }
        public long date { get; set; }
        public string value { get; set; }
        public string name { get; set; }
    }


    internal class Program
    {
        static readonly HttpClient client = new HttpClient();
        static async Task Main(string[] args)
        {
            // The starting string of the API, parameters to be chosen using the website: https://opendata.smhi.se/apidocs/metobs/index.html
            string apiUrl = "https://opendata-download-metobs.smhi.se/api/version/latest";

            // Main menu method keeping Main() as tidy as possible
            await MainMenu(apiUrl);
        }

        static async Task MainMenu(string apiUrl)
        {
            string[] options = { "Current Average Temperature in Sweden", "Rainfall in Lund last month", "Print temperature of all stations", "Exit" };
            int currentOptionIndex = 0;
            bool exit = false;

            // While loop to go through choices based on above array
            while (!exit)
            {
                // Print the menu options
                for (int i = 0; i < options.Length; i++)
                {
                    if (i == currentOptionIndex)
                    {
                        Console.ForegroundColor = ConsoleColor.Black;
                        Console.BackgroundColor = ConsoleColor.White;
                    }

                    Console.WriteLine(options[i]);

                    Console.ResetColor();
                }

                // Wait for user input
                ConsoleKeyInfo keyInfo = Console.ReadKey(true);
                Console.Clear();

                // Move the selection up
                if (keyInfo.Key == ConsoleKey.UpArrow)
                {
                    currentOptionIndex--;
                    if (currentOptionIndex < 0)
                    {
                        currentOptionIndex = options.Length - 1;
                    }
                }

                // Move the selection down
                if (keyInfo.Key == ConsoleKey.DownArrow)
                {
                    currentOptionIndex++;
                    if (currentOptionIndex >= options.Length)
                    {
                        currentOptionIndex = 0;
                    }
                }

                // Call the selected option
                if (keyInfo.Key == ConsoleKey.Enter)
                {
                    if (currentOptionIndex == 3)
                        Environment.Exit(0);

                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine("\nCall has been made, please wait for a response from the API.");
                    Console.ResetColor();
                    await CallOption(currentOptionIndex, apiUrl);
                }
            }
        }

        static async Task CallOption(int option, string apiUrl)
        {
            // Strings used for specific requests through the API.
            // Different .net environment will eventually be preferred for easier deserialisation.
            string apiAllStationTemps = "/parameter/39/station-set/all/period/latest-hour/data.json?measuringStations=all";
            string apiLundRainFall = "/parameter/23/station/53430/period/latest-months/data.json";

            StationData stationDataResponse;
            Station stationResponse;

            switch (option)
            {
                case 0: // Average temperature across sweden
                    stationDataResponse = await GetStationData(apiUrl, apiAllStationTemps);
                    TemperaturesSweden(stationDataResponse, true);
                    break;
                case 1: // Average rainfall in Lund last 4 months
                    stationResponse = await GetRainfallData(apiUrl, apiLundRainFall);
                    GetLundRainfall(stationResponse);
                    break;
                case 2: // Printing the temperature of each station, one by one
                    stationDataResponse = await GetStationData(apiUrl, apiAllStationTemps);
                    TemperaturesSweden(stationDataResponse, false);
                    break;
            }

            // Ending sequence showing up after each method has done its thing, avoiding code repetition
            Console.WriteLine("\nPress any key to return to the menu.");
            Console.ReadKey();
            Console.Clear();
        }

        static async Task<Station> GetRainfallData(string apiUrl, string additionUrl)
        {
            string target = $"{apiUrl}{additionUrl}";
            string response = await client.GetStringAsync(target);

            Station data = JsonSerializer.Deserialize<Station>(response);

            return data;
        }

        static async Task<StationData> GetStationData(string apiUrl, string additionUrl)
        {
            string target = $"{apiUrl}{additionUrl}";
            string response = await client.GetStringAsync(target);

            StationData data = JsonSerializer.Deserialize<StationData>(response);

            return data;
        }

        static void TemperaturesSweden(StationData apiResponse, bool averageAll)
        {
            double sum = 0.0;
            int count = 0;

            Console.Clear();

            // Creating cancellationtoken here after making sure you are printing a lot (aka not getting the average)
            // Upon pressing escape, you create the token
            CancellationTokenSource cts = new CancellationTokenSource();
            Task.Factory.StartNew(() =>
            {
                try
                {
                    while (!cts.Token.IsCancellationRequested && Console.ReadKey(true).Key != ConsoleKey.Escape) { }
                    cts.Cancel();
                }
                catch
                {
                    return;
                }
            });

            // Try block for the cts, in case it breaks
            try
            {
                foreach (Station station in apiResponse.station)
                {
                    if (station.value == null) continue; // Some stations have not-working temperatures. These return as null.
                    foreach (Value value in station.value)
                    {
                        if (cts.Token.IsCancellationRequested) // Cancel requested
                        {
                            Console.ForegroundColor = ConsoleColor.DarkRed;
                            Console.WriteLine("The task has been cancelled by the user.");
                            Console.ResetColor();
                            return;
                        }

                        if (averageAll && double.TryParse(value.value, out double temperature)) // You want the average of everything
                        {
                            sum += temperature;
                            count++;
                        }
                        else if (double.TryParse(value.value, out temperature)) // Printing individuals if average isn't needed
                        {
                            if (count == 0) // Basic info about how to cancel the printings
                            {
                                Console.ForegroundColor = ConsoleColor.DarkRed;
                                Console.WriteLine("Press escape to cancel printing.");
                                Console.ResetColor();
                                count++;
                            }

                            // Printings with a sleep method to not spam the user with blocks of text instantly
                            Console.WriteLine($"{station.name}: {value.value}");
                            Thread.Sleep(100);
                        }
                    }
                }

                if (averageAll) // Clean the screen, show what matters.
                {
                    Console.Clear();
                    double averageTemperature = sum / count;
                    Console.WriteLine($"The average temperature: {averageTemperature:F2}°C");
                    cts.Dispose();
                }
            }
            catch (OperationCanceledException) // Error catching
            {
                Console.ForegroundColor = ConsoleColor.DarkRed;
                Console.WriteLine("The task has been cancelled.");
                Console.ResetColor();
            }
            finally
            {
                cts.Dispose();
            }
            return;
        }

        static void GetLundRainfall(Station response)
        {
            double total = 0;
            string startDate = "";
            string endDate = "";

            foreach (Value value in response.value)
                total += Convert.ToDouble(value.value);

            for (int i = 0; i < 2; i++)
            {
                // If first iteration, take earliest "from", else take latest "to"
                long unixTimeStamp = i == 0 ? response.value[0].from : response.value[response.value.Length - 1].to;
                DateTimeOffset dateTimeOffset = DateTimeOffset.FromUnixTimeMilliseconds(unixTimeStamp);
                DateTimeOffset swedishDateTimeOffset = dateTimeOffset.ToLocalTime(); // Convert to Swedish local time
                string res = swedishDateTimeOffset.ToString("d", new System.Globalization.CultureInfo("sv-SE"));

                // Set values based on whether it's the first or second run
                _ = i == 0 ? startDate = res : endDate = res;
            }

            Console.Clear();
            Console.WriteLine($"The total rainfall in Lund between {startDate} and {endDate} was: {total} millimeters.");
        }
    }
}