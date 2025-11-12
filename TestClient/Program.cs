using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
//using System.Net.Http.Json; // Required for PostAsJsonAsync
using System.Threading.Tasks;



using (var client = new HttpClient())
{
    //int timeOutInSec = 60;
    //var handler = new SocketsHttpHandler
    //{
    //    ConnectTimeout = TimeSpan.FromSeconds(timeOutInSec),
    //};

    //using var httpClient = new HttpClient(handler);
    //httpClient.Timeout = TimeSpan.FromSeconds(timeOutInSec);

    using var httpClient = new HttpClient();

    var apiUrl1 = "https://localhost:7231/api/Processing/LongRunning"; 
    var apiUrl2 = "https://localhost:7231/api/SimpleRequest/process/true";
    var apiUrl3 = "https://localhost:7231/api/SimpleRequest/process/false";
    var dataToSend1 = new LongRunningRequest(1, "" );
    string jsonContent1 = JsonSerializer.Serialize(dataToSend1);
    var dataToSend2 = new SimpleRequest(100, "");
    string jsonContent2 = JsonSerializer.Serialize(dataToSend2);
    var dataToSend3 = new SimpleRequest(1, "");
    string jsonContent3 = JsonSerializer.Serialize(dataToSend3);

    // Create StringContent with the JSON string, encoding, and media type
    var content1 = new StringContent(jsonContent1, Encoding.UTF8, "application/json");
    var content2 = new StringContent(jsonContent2, Encoding.UTF8, "application/json");
    var content3 = new StringContent(jsonContent3, Encoding.UTF8, "application/json");

    IList<Task> tasks = new List<Task>();  
    for (int i = 0; i < 24; i++)  //QueuedLongRunningProcessor
    {
        var task1 = Task.Run(() => httpClient.PostAsync(apiUrl1, content1));
        tasks.Add(task1);
    }

    //IList<Task> tasks2 = new List<Task>();
    for (int i = 0; i < 24; i++) //SimplePeocessor (100 inner iterations)
    {
        var task2 = Task.Run(() => httpClient.PostAsync(apiUrl2, content2));
        tasks.Add(task2);
    }

    //IList<Task> tasks3 = new List<Task>();
    for (int i = 0; i < 2; i++) //SimplePeocessor (1 inner iteration)
    {
        var task3 = Task.Run(() => httpClient.PostAsync(apiUrl3, content3));
        tasks.Add(task3);
    }

    await Task.WhenAll(tasks);
    //await Task.WhenAll(tasks1);
    //await Task.WhenAll(tasks2);
    //await Task.WhenAll(tasks3);
   // Console.ReadLine();
    //await Task.Delay(5000);
    //for (int i = 0;i < 100;i++)
    //{
    //    await Task.Delay(10);
    //}


    // Send the POST request

    //HttpResponseMessage response1 = await httpClient.PostAsync(apiUrl1, content1);

    //HttpResponseMessage response2 = await httpClient.PostAsync(apiUrl2, content2);


    //if (response1.IsSuccessStatusCode)
    //{
    //    Console.WriteLine("Data posted successfully!");
    //    string responseContent = await response1.Content.ReadAsStringAsync();
    //    Console.WriteLine($"Response1: {responseContent}");
    //}
    //else
    //{
    //    Console.WriteLine($"Error1: {response1.StatusCode} - {await response1.Content.ReadAsStringAsync()}");
    //}

    //if (response2.IsSuccessStatusCode)
    //{
    //    Console.WriteLine("Data posted successfully!");
    //    string responseContent = await response2.Content.ReadAsStringAsync();
    //    Console.WriteLine($"Response2: {responseContent}");
    //}
    //else
    //{
    //    Console.WriteLine($"Error2: {response1.StatusCode} - {await response1.Content.ReadAsStringAsync()}");
    //}
}


public record LongRunningRequest(int Iterations, string Data);
public record SimpleRequest(int Iterations, string Data);























// See https://aka.ms/new-console-template for more information
//onsole.WriteLine("Hello, World!");
