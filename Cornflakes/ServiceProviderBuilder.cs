﻿using Cornflakes.LifetimeStrategies;

namespace Cornflakes
{
    public class ServiceProviderBuilder : IServiceProviderBuilder
    {
        private readonly ServiceProvider serviceProvider = new ServiceProvider();

        public IServiceProviderBuilder RegisterService<TService>(ILifetimeStrategy creationStrategy)
        {
            this.serviceProvider.RegisterService(new ServiceDescriptor(
                typeof(TService),
                creationStrategy
            ));
            return this;
        }

        public IServiceProviderBuilder RegisterServices(IServiceCollection services)
        {
            foreach (var service in services)
            {
                this.serviceProvider.RegisterService(service);
            }
            return this;
        }

        public IServiceProvider Build()
        {
            return this.serviceProvider;
        }

    }
}
