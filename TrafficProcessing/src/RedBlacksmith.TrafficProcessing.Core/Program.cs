using RedBlacksmith.TrafficProcessing.Kafka;
var builder = WebApplication.CreateBuilder(args);


var services = builder.Services;



services.AddKafkaServices();

var app = builder.Build();




app.Run();

