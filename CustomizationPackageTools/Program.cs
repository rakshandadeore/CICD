using System.CommandLine;
using System.CommandLine.NamingConventionBinder;
using System.IO.Compression;
using System.ServiceModel;
using System.Xml;
using System.Text.Json;
using System.Text;
using System.Net.Http.Headers;
using System.Reflection.Metadata.Ecma335;
using System.Runtime.CompilerServices;
using ServiceGate;
using System.Security.Policy;



namespace Velixo.Common.CustomizationPackageTools
{
    class Program
    {
        static async Task Main(string[] args)
        {
           try
            {

                Console.WriteLine(Environment.CommandLine);

                var rootCommand = new RootCommand();
                var buildCommand = new Command("build")
            {
                new Option<string>("--customizationpath", "The folder containing the customization source code (_project folder).") { IsRequired = true},
                new Option<string>("--packagefilename", "The name of the customization package.") { IsRequired = true},
                new Option<string>("--description", "The description of the customization project.") { IsRequired = true},
                new Option<int>("--level", "The number representing the level that is used to resolve conflicts that arise if multiple modifications of the same items of the website are merged. Defaults to 0."),
            };
                rootCommand.Add(buildCommand);

                var publishCommand = new Command("publish")
            {
                new Option<string>("--packagefilename", "The name of the customization package file.") { IsRequired = true},
                new Option<string>("--packagename", "The name of the customization.") { IsRequired = true},
                new Option<string>("--url", "The root URL of the Acumatica website where the customization should be published.") { IsRequired = true},
                new Option<string>("--username", "The username to connect.") { IsRequired = true},
                new Option<string>("--password", "The password to conect.") { IsRequired = true},
                new Option<string>("--description", "The description of the customization project.") { IsRequired = true},
                new Option<int>("--level", "The customization level.") { IsRequired = true},

            };
                rootCommand.Add(publishCommand);

                buildCommand.Handler = CommandHandler.Create((string customizationPath, string packageFilename, string description, int level) =>
                {
                    Console.WriteLine($"Generating customization package {packageFilename}...");
                    BuildCustomizationPackage(customizationPath, packageFilename, description, level);
                    Console.WriteLine("Done!");
                });

                publishCommand.Handler = CommandHandler.Create(async (string packageFilename, string packageName, string url, string username, string password, string description, int level) =>
                {
                    Console.WriteLine($"Publishing customization package {packageFilename} to {url}...");
                    await PublishCustomizationPackage(packageFilename, packageName, url, username, password, description, level);
                    Console.WriteLine("Done!");
                });

                await rootCommand.InvokeAsync(args);
           }
            catch (Exception ex)
            {
                Console.WriteLine($"FATAL ERROR: {ex.Message}");
                Environment.Exit(1);
            }
        }

        private static void BuildCustomizationPackage(string customizationPath, string packageFilename, string description, int level)
        {
            // Our poor man's version of PX.CommandLine.exe -- to keep things simple.
            var projectXml = new XmlDocument();
            var customizationNode = projectXml.CreateElement("Customization");

            customizationNode.SetAttribute("level", level.ToString());
            customizationNode.SetAttribute("description", description);
            customizationNode.SetAttribute("product-version", "20.202");

            // Append all .xml files to project.xml
            foreach (var file in Directory.GetFiles(Path.Combine(customizationPath, "_project"), "*.xml"))
            {
                 Console.WriteLine($"===========...",file);

                if (file.EndsWith("ProjectMetadata.xml")) continue;

                Console.WriteLine($"Appending {Path.GetFileName(file)} to customization project.xml...");
                var currentFileXml = new XmlDocument();
                currentFileXml.Load(file);

                if (currentFileXml.DocumentElement == null) throw new Exception("project.xml empty");

                customizationNode.AppendChild(projectXml.ImportNode(currentFileXml.DocumentElement, true));
            }

            //Append other customization assets to zip file
            using (FileStream zipToOpen = new FileStream(packageFilename, FileMode.Create))
            {
                using (ZipArchive archive = new ZipArchive(zipToOpen, ZipArchiveMode.Create))
                {
                    //Append every other files directly, flattening the file name in the process
                    foreach (var directory in Directory.GetDirectories(customizationPath))
                    {
                        if (directory.EndsWith(@"\_project")) continue;
                        AddAssetsToPackage(archive, directory, customizationPath, customizationNode);
                    }

                    projectXml.AppendChild(customizationNode);
                    ZipArchiveEntry projectFile = archive.CreateEntry("project.xml", CompressionLevel.Optimal);
                    using (StreamWriter writer = new StreamWriter(projectFile.Open()))
                    {
                        projectXml.Save(writer);
                    }
                }
            }
        }

        private static void AddAssetsToPackage(ZipArchive archive, string currentDirectory, string rootDirectory, XmlElement customizationElement)
        {
            Console.WriteLine($"Processing directory {currentDirectory}...");
 Console.WriteLine($"===========...11111",currentDirectory);
            foreach (var file in Directory.GetFiles(currentDirectory))
            {
                Console.WriteLine($"===========...11111",file);
                string targetZipFileName = file.Substring(rootDirectory.Length).Substring(1);
                Console.WriteLine($"Adding {targetZipFileName} to customization project...");

                archive.CreateEntryFromFile(file, targetZipFileName, CompressionLevel.Optimal);

                //Add reference to customization project as well
                var fileElement = customizationElement.OwnerDocument.CreateElement("File");
                fileElement.SetAttribute("AppRelativePath", targetZipFileName);
                customizationElement.AppendChild(fileElement);
            }

            foreach (var directory in Directory.GetDirectories(currentDirectory))
            {
                AddAssetsToPackage(archive, directory, rootDirectory, customizationElement);
            }
        }

        private static async Task PublishCustomizationPackage(string packageFilename, string packageName, string url, string username, string password, string description, int level)
        {
            try
            {

                BasicHttpBinding binding = new BasicHttpBinding() { AllowCookies = true };
                binding.Security.Mode = BasicHttpSecurityMode.None;
                binding.OpenTimeout = new TimeSpan(0, 10, 0);
                binding.SendTimeout = new TimeSpan(0, 10, 0);
                binding.ReceiveTimeout = new TimeSpan(0, 10, 0);

                EndpointAddress address = new EndpointAddress(url + "/api/ServiceGate.asmx");
                var gate = new ServiceGate.ServiceGateSoapClient(binding, address);

                Console.WriteLine($"\nLogging in to {url}...");
                var cookies = await AcumaticaLogin(url, username, password);

                if (cookies == null || cookies.Count == 0)
                {
                    Console.WriteLine("Login failed! Stopping execution.");
                    return;
                }

                Console.WriteLine($"\nImporting the package...");
                await ImportCustomization(cookies, packageName, description, level, url);

                Console.WriteLine($"\nFetching already published projects...");
                string allProjectNamesString = await GetPublished(url, cookies, packageName);
                string[] allProjectNames = allProjectNamesString.Split(',', StringSplitOptions.RemoveEmptyEntries);
                Console.WriteLine($"\nProjects to published: {string.Join(", ", allProjectNames)}");

                Console.WriteLine($"\nPublishing customization project...");
                await PublishBegin(url, cookies, allProjectNames);

                bool isPublished = await CheckPublishStatus(url, cookies);

                if (isPublished)
                {
                    Console.WriteLine("------Build publish Successfully-------");
                }
                else
                {
                    Console.WriteLine("Publish failed or timed out.");
                }


                Console.WriteLine($"Logging out...");
                await gate.LogoutAsync();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"ERROR: {ex.Message}");
                Environment.Exit(1); 
            }
        }

        private static async Task<List<string>> AcumaticaLogin(string baseUrl, string username, string password)
        {
            using (HttpClientHandler handler = new HttpClientHandler { UseCookies = false })
            using (HttpClient client = new HttpClient(handler))
            {
                string url = $"{baseUrl}/entity/auth/login";

                var data = new
                {
                    name = username,
                    password = password,
                    locale = "EN-US"
                };

                try
                {
                    var jsonContent = new StringContent(JsonSerializer.Serialize(data), Encoding.UTF8, "application/json");

                    HttpResponseMessage response = await client.PostAsync(url, jsonContent);

                    if (response.IsSuccessStatusCode)
                    {
                        Console.WriteLine("Login successful!");

                        List<string> cookies = new List<string>();
                        if (response.Headers.TryGetValues("Set-Cookie", out var setCookies))
                        {
                            cookies = setCookies.Select(cookie => cookie.Split(';')[0]).ToList();
                        }

                        if (cookies.Count == 0)
                        {
                            Console.WriteLine("Login succeeded but no cookies were received.");
                            //return null;
                            return new List<string>(); // Avoid returning null

                        }

                        return cookies;
                    }
                    else
                    {
                        Console.WriteLine($"Login failed: {response.StatusCode} {response.ReasonPhrase}");
                        //return null;
                        return new List<string>(); // Avoid returning null
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error logging in: {ex.Message}");
                    //return null;
                    return new List<string>(); // Avoid returning null
                }
            }
        }

        private static async Task ImportCustomization(List<string> cookies, string projectName, string description, int level, string baseUrl)
        {
            string packagePath = Path.Combine($"./Customization/{projectName}", $"{projectName}.zip");

            if (!File.Exists(packagePath))
            {
                Console.WriteLine($"Error: File not found at path: {packagePath}");
                return;
            }

            string packageContent = Convert.ToBase64String(await File.ReadAllBytesAsync(packagePath));

            using (HttpClient client = new HttpClient())
            {
                client.DefaultRequestHeaders.Add("Cookie", string.Join("; ", cookies));

                var payload = new
                {
                    projectLevel = level,
                    isReplaceIfExists = true,
                    projectName = projectName,
                    projectDescription = description,
                    projectContentBase64 = packageContent
                };

                var content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");

                try
                {
                    HttpResponseMessage response = await client.PostAsync($"{baseUrl}/CustomizationApi/import", content);

                    if (response.IsSuccessStatusCode)
                    {
                        Console.WriteLine("Import successful!");
                    }
                    else
                    {
                        Console.WriteLine($"Import failed: {response.StatusCode} {response.ReasonPhrase}");
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error calling import: {ex.Message}");
                }
            }
        }


        private static async Task<string> GetPublished(string url, List<string> cookies, string newProject)
        {
            using (var client = new HttpClient())
            {
                if (cookies == null || cookies.Count == 0)
                {
                    Console.WriteLine("WARNING: No cookies found! Authentication may have failed.");
                }
                else
                {
                    Console.WriteLine("Fetching project list....");
                }

                client.DefaultRequestHeaders.Add("Cookie", string.Join("; ", cookies ?? new List<string>()));

                // Create an empty JSON body (most APIs expect this for POST requests)
                var content = new StringContent("{}", Encoding.UTF8, "application/json");

                try
                {
                    HttpResponseMessage response = await client.PostAsync($"{url}/CustomizationApi/GetPublished", content);

                    if (response.IsSuccessStatusCode)
                    {
                        var jsonResponse = await response.Content.ReadAsStringAsync();
 Console.WriteLine("list....",jsonResponse+",");
                        try
                        {
                            var jsonObject = JsonDocument.Parse(jsonResponse).RootElement;

                            if (jsonObject.TryGetProperty("projects", out JsonElement projectsElement))
                            {
                              
                                var projects = projectsElement
                                    .EnumerateArray()
                                    .Select(p => p.GetProperty("name").GetString())
                                    .Where(name => !string.IsNullOrEmpty(name) && !name.Contains("SimpleCustomization"))
                                    .ToList();

                                projects.Add(newProject);
                             
                                return string.Join(",", projects);
                            }
                            else
                            {
                                Console.WriteLine("No 'projects' field found in API response.");
                                return newProject;
                            }
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"Error parsing JSON response: {ex.Message}");
                            return newProject;
                        }
                    }
                    else
                    {
                        Console.WriteLine($"Failed to fetch published projects. HTTP Status: {response.StatusCode}");
                        var errorMessage = await response.Content.ReadAsStringAsync();
                        Console.WriteLine($"Error details: {errorMessage}");
                        return newProject;
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Exception while calling GetPublished: {ex.Message}");
                    return newProject;
                }
            }
        }


        private static async Task PublishBegin(string url, List<string> cookies, string[] projectNames)
        {
            using (var client = new HttpClient())
            {
                client.DefaultRequestHeaders.Add("Cookie", string.Join("; ", cookies));

                var payload = new
                {
                    isMergeWithExistingPackages = false,
                    isOnlyValidation = false,
                    isOnlyDbUpdates = false,
                    isReplayPreviouslyExecutedScripts = false,
                    projectNames = projectNames,
                    tenantMode = "Current"
                };

                var content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
                var response = await client.PostAsync($"{url}/CustomizationApi/PublishBegin", content);

                if (!response.IsSuccessStatusCode)
                {
                    Console.WriteLine($"PublishBegin failed! HTTP {response.StatusCode}");
                    var errorMessage = await response.Content.ReadAsStringAsync();
                    Console.WriteLine($"Error details: {errorMessage}");
                }
                else
                {
                    Console.WriteLine("PublishBegin API executed successfully.");
                }
            }
        }

        private static async Task<bool> CheckPublishStatus(string baseUrl, List<string> cookies)
        {
            string url = $"{baseUrl}/CustomizationApi/PublishEnd"; // Ensure URL is correct
            bool isCompleted = false;
            int attempt = 0;
            int maxAttempts = 40; // Max 38 attempts (~30 min timeout)
            int delay = 12000; // Start with 2 min delay
            var startTime = DateTime.UtcNow;

            using (var client = new HttpClient())
            {
                client.DefaultRequestHeaders.Add("Cookie", string.Join("; ", cookies));
                client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

                while (!isCompleted)
                {
                    try
                    {
                        var content = new StringContent("{}", Encoding.UTF8, "application/json");
                        HttpResponseMessage response = await client.PostAsync(url, content);

                        if (response.IsSuccessStatusCode)
                        {
                            var jsonResponse = await response.Content.ReadAsStringAsync();
                            var jsonObject = JsonDocument.Parse(jsonResponse).RootElement;

                            isCompleted = jsonObject.GetProperty("isCompleted").GetBoolean();
                            bool isFailed = jsonObject.GetProperty("isFailed").GetBoolean();

                            Console.WriteLine($"[Attempt {attempt + 1}] Publish Status: Completed={isCompleted}, Failed={isFailed}");

                            if (isFailed)
                            {
                                string errorMessage = jsonObject.TryGetProperty("log", out JsonElement log) && log.GetArrayLength() > 0
                                    ? log[0].GetProperty("message").GetString() ?? "Unknown error"
                                    : "Unknown error";

                                Console.WriteLine($"Publish failed! Reason: {errorMessage}");
                                return false;
                            }

                            if (isCompleted)
                            {
                                double totalTime = (DateTime.UtcNow - startTime).TotalMinutes;
                                Console.WriteLine($"Customization package published successfully in {Math.Round(totalTime)} minutes!");
                                return true;
                            }
                        }
                        else
                        {
                            string errorResponse = await response.Content.ReadAsStringAsync();
                            throw new Exception($"Error checking publish status: {response.StatusCode} - {response.ReasonPhrase}\nResponse Body: {errorResponse}");
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Exception checking publish status: {ex.Message}");
                        throw;
                    }

                    // Adjust delay dynamically based on attempts
                    if (attempt >= 3 && attempt < 8) delay = 60000;  // After 3 attempts (~5 min), poll every 1 min
                    else if (attempt >= 8 && attempt < 18) delay = 30000; // After 8 attempts (~10 min), poll every 30 sec
                    else if (attempt >= 18) delay = 15000;  // After 18 attempts (~15 min), poll every 15 sec

                    await Task.Delay(delay);
                    attempt++;
                }

                Console.WriteLine("Publishing timeout reached (30 min).");
                return false;
            }
        }

    }
}