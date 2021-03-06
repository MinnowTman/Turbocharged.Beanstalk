Turbocharged.Beanstalk
======================

[![Build status](https://ci.appveyor.com/api/projects/status/9ydx1vwh8hjxhv4w?svg=true)](https://ci.appveyor.com/project/jennings/turbocharged-beanstalk)
[![NuGet](https://img.shields.io/nuget/v/Turbocharged.Beanstalk.svg)](http://www.nuget.org/packages/Turbocharged.Beanstalk/)

A [Beanstalk][beanstalk] .NET client library filled with `async` happiness.

Don't like `async`? That's cool, no problem. You might like
[libBeanstalk.NET][libbeanstalk] instead.


Usage
-----

Do the normal thing:

    PM> Install-Package Turbocharged.Beanstalk

### Producing Jobs

    // Jobs are byte arrays
    byte[] job = new byte[] { 102, 105, 101, 116, 123, 124, 101, 114, 113 };

    // A producer exposes methods used for inserting jobs
    // Most producer methods are affected by UseAsync(tube)

    IProducer producer = await BeanstalkConnection.ConnectProducerAsync("localhost:11300");
    await producer.UseAsync("mytube");
    await producer.PutAsync(job, 5, TimeSpan.Zero, TimeSpan.FromSeconds(30));

    // You can also put custom objects and they'll be serialized to JSON
    await producer.PutAsync<MyObject>(obj, 5, TimeSpan.Zero, TimeSpan.FromSeconds(30));

    producer.Dispose();

### Consuming jobs

    // A consumer exposes methods for reserving and deleting jobs
    // Most consumer methods are affected by WatchAsync(tube)

    IConsumer consumer = await BeanstalkConnection.ConnectConsumerAsync("localhost:11300");
    await consumer.WatchAsync("mytube");
    Job job = await consumer.ReserveAsync();

    // You can also deserialize if you know what type you're expecting
    // Job<MyObject> job = await consumer.ReserveAsync<MyObject>();

    // ...work work work...

    if (success)
        await consumer.DeleteAsync(job.Id);
    else
        await consumer.BuryAsync(job.Id, priority: 5);

    consumer.Dispose();

### Creating a worker task

    Func<IWorker, Job, Task> workerFunc = async (worker, job) =>
    {
        // ... work with the job...
        if (success)
            await worker.DeleteAsync(job.Id);
        else
            await worker.BuryAsync(job.Id, 1);
    };

    IDisposable worker = BeanstalkConnection.ConnectWorkerAsync(hostname, port, workerFunc);

    // Or, typed:

    Func<IWorker, Job<T>, Task> typedWorkerFunc = ...;

    IDisposable typedWorker = BeanstalkConnection.ConnectWorkerAsync<MyObject>(hostname, port, typedWorkerFunc);

    // When you're ready to stop the workers
    worker.Dispose();
    typedWorker.Dispose();

A worker task is a dedicated BeanstalkConnection. You provide a delegate with
signature `Func<IWorker, Job, Task>` to process jobs and delete/bury them when
finished. Turbocharged.Beanstalk calls "reserve" for you and hands the reserved
job to your delegate.


Goals
-----

* Simple API that encourages ease of use
* Teach myself how to properly use the shiny asynchrony features in C# 5.0.


License
-------

The MIT License. See `LICENSE.md`.


[beanstalk]: http://kr.github.io/beanstalkd/
[libbeanstalk]: https://github.com/sdether/libBeanstalk.NET
