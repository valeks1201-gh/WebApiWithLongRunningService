During WebApi projects development the following issue was noticed. 
For example, a slow request (typically 0.5-1 second) is initiated. Approximately at the same time, the server receives many (e.g., hundreds or thousands) fast requests. 
The server automatically starts processing the fast requests, deferring the execution of the slow one. As a result, the execution time of the slow request can extend to, for example, 40-50 seconds.
A temporary solution can be applied: clients avoid unnecessarily calling server endpoints where possible. No possibility to use better server hardware to increase performance. 
In principle, additional measures can also be applied on the server side. 
For instance, code can be implemented to ensure that the CPU core that starts processing a slow request is not interrupted by other requests. 
For example, 50% of CPU cores can be allocated specifically for slow requests. If there are also many slow requests, they can be queued when no dedicated CPU cores are available. 
Subsequently, the BackgroundWorker will process these requests from the queue.
A small solution was developed in Visual Studio to analyze the impact of multiple fast client requests on the execution time of slow requests. 
The solution consists of a WebApi project and a console application for making requests to this WebApi using the HTTP client.
In the WebApi project, request processing time is emulated using await Task.Delay(t). For a slow request, t=1000 milliseconds; for a fast one, t=1.
In the first slow request endpoint, the message is first created in a queue. 
Then a HostedService (essentially a Background Worker) processes this message from the queue, assigning it to an available CPU core from a dedicated pool. 
This CPU core works on this slow request without being interrupted by other incoming requests. If no dedicated CPU core is free, the HostedService waits for one to become available.
In the second slow request endpoint, no additional steps are taken: the OS assigns this request to any available CPU core for execution.
In the fast request endpoint, the process is similar: the OS assigns the request to any available CPU core.
When processing a request to each slow endpoint, the actual request execution time is logged.
The development machine has 12 CPU cores in total, half of which can be allocated for processing requests to the first slow endpoint.
The client, in an asynchronous loop, sends 24 slow requests to both the first and second endpoints.
If only a couple of fast requests are sent in an asynchronous loop alongside these, the logs show the following typical performance picture:

[INF] ProcessingQueueService.ProcessSingleRequestAsync. TotalDuration=1053,2481. processorId=1
[INF] ProcessingQueueService.ProcessSingleRequestAsync. TotalDuration=1042,3864. processorId=0
[INF] ProcessingQueueService.ProcessSingleRequestAsync. TotalDuration=1039,0586. processorId=3
[INF] SimpleProcessor.ProcessSimpleTaskAsync. TotalDuration=1013,171
[INF] SimpleProcessor.ProcessSimpleTaskAsync. TotalDuration=1036,0003
[INF] SimpleProcessor.ProcessSimpleTaskAsync. TotalDuration=1009,2777.
The lines above with ProcessingQueueService correspond to the first slow request endpoint, and SimpleProcessor to the second one. 
For the ProcessingQueueService lines, the ID of the CPU core that processed the request is also displayed.
However, if 2000 fast requests are sent in an asynchronous loop, the logs show this typical performance picture:
[INF] ProcessingQueueService.ProcessSingleRequestAsync. TotalDuration=1046,1633. processorId=0
[INF] ProcessingQueueService.ProcessSingleRequestAsync. TotalDuration=1044,368. processorId=2
[INF] ProcessingQueueService.ProcessSingleRequestAsync. TotalDuration=1169,1916. processorId=3
[INF] SimpleProcessor.ProcessSimpleTaskAsync. TotalDuration=4989,3104
[INF] SimpleProcessor.ProcessSimpleTaskAsync. TotalDuration=5174,9946
[INF] SimpleProcessor.ProcessSimpleTaskAsync. TotalDuration=5214,4269.
Analysis of the obtained data shows that without special measures, multiple fast requests can significantly (in this case, approximately 5 times) increase the execution time of a slow request.
However, if the slow request is processed by a dynamically assigned dedicated CPU core, its execution time remains largely unaffected by the number of fast requests arriving at the server.



