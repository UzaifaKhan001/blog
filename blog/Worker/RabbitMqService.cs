using RabbitMQ.Client;
using System;
using System.Text;
using Microsoft.Extensions.Configuration;

namespace blog.Services
{
    public class RabbitMqService : IDisposable
    {
        private readonly IConnection _connection;
        private readonly IModel _channel;
        private readonly string _queueName = "orders";

        public RabbitMqService(IConfiguration config)
        {
            var section = config.GetSection("RabbitMq");
            var factory = new ConnectionFactory
            {
                HostName = section.GetValue<string>("HostName") ?? "localhost",
                Port = section.GetValue<int?>("Port") ?? 5672,
                UserName = section.GetValue<string>("UserName") ?? "guest",
                Password = section.GetValue<string>("Password") ?? "guest",
                VirtualHost = section.GetValue<string>("VirtualHost") ?? "/",
                RequestedConnectionTimeout = TimeSpan.FromSeconds(10)
            };

            try
            {
                _connection = factory.CreateConnection();
                _channel = _connection.CreateModel();

                _channel.QueueDeclare(
                    queue: _queueName,
                    durable: false,
                    exclusive: false,
                    autoDelete: false,
                    arguments: null
                );

                Console.WriteLine($"✅ Connected to RabbitMQ at {factory.HostName}:{factory.Port}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Failed to connect to RabbitMQ: {ex.Message}");
                throw;
            }
        }

        public void Publish(string message)
        {
            if (_channel == null || !_connection.IsOpen)
            {
                Console.WriteLine("⚠️ RabbitMQ connection not open — message not sent.");
                return;
            }

            var body = Encoding.UTF8.GetBytes(message);
            _channel.BasicPublish(
                exchange: "",
                routingKey: _queueName,
                basicProperties: null,
                body: body
            );

            Console.WriteLine($"📨 [RabbitMQ] Sent: {message}");
        }

        public void Dispose()
        {
            _channel?.Close();
            _connection?.Close();
        }
    }
}
