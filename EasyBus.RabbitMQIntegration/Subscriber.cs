﻿using EasyNetQ;
using Newtonsoft.Json;
using System;
using System.Configuration;
using System.Linq;

namespace EasyBus.RabbitMQIntegration
{
	public class Subscriber : EasyBus.Contracts.ISubscriber
	{
		#region Fields

		private global::SimpleInjector.Container container;
		private IBus bus;

		public string ErrorExchange { get; set; }

		public string ErrorQueue { get; set; }

		#endregion Fields

		#region Methods

		public void Subscribe(EasyBus.Contracts.IMessageHandler handler)
		{
			bus = container.GetInstance<IRabbitBus>().Bus;
			// Get queue name.
			var queueName = handler.QueueName;
			var queue = bus.Advanced.QueueDeclare(queueName);

			bus.Advanced.Consume<string>(queue, (msg, info) =>
			{
				try
				{
					Process(handler, msg);
				}
				catch (Exception exc)
				{
					HandleConsumerError(queue, exc);
				}
			});
		}

		public void Process(EasyBus.Contracts.IMessageHandler handler, IMessage<string> msg)
		{
			var messsageType = handler.GetType().BaseType.GetGenericArguments().First();
			var payload = JsonConvert.DeserializeObject(msg.Body, messsageType) as EasyBus.Contracts.IMessage;
			payload.CorrelationId = msg.Properties.CorrelationId;

			handler.Handle(payload);
		}

		private void HandleConsumerError(EasyNetQ.Topology.IQueue queue, Exception exc)
		{
			bus.Advanced.Container.Resolve<IConventions>().ErrorExchangeNamingConvention = x => ErrorExchange + queue.Name + "Message";
			bus.Advanced.Container.Resolve<IConventions>().ErrorQueueNamingConvention = () => ErrorQueue + queue.Name;
			throw exc;
		}

		public Subscriber(SimpleInjector.Container container)
		{
			// TODO: Complete member initialization
			ErrorQueue = ConfigurationManager.AppSettings.Get("ErrorQueueNamingConvention");
			ErrorExchange = ConfigurationManager.AppSettings.Get("ErrorExchangeNamingConvention");

			this.container = container;
		}

		#endregion Methods

		public void Subscribe(EasyBus.Contracts.IResponse response)
		{
		}

		public void Response<TRequest, TResponse>(EasyBus.Contracts.IResponseMessageHandler handler)
			where TRequest : class
			where TResponse : class
		{
			bus = container.GetInstance<IRabbitBus>().Bus;
			bus.Respond<TRequest, TResponse>(
				rq => Response<TRequest, TResponse>(rq, handler));
		}

		private TResponse Response<TRequest, TResponse>(TRequest request, EasyBus.Contracts.IResponseMessageHandler handler)
			where TRequest : class
			where TResponse : class
		{
			EasyBus.Contracts.IRequestMessage message = request as EasyBus.Contracts.IRequestMessage;
			return handler.Handle(message) as TResponse;
		}

		public void Response<TRequest, TResponse>(Func<TRequest, TResponse> onResponse)
			where TRequest : class
			where TResponse : class
		{
		}
	}
}