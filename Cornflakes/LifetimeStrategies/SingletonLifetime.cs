﻿namespace Cornflakes.LifetimeStrategies
{
    internal class SingletonLifetime : ILifetimeStrategy
    {
        private object instance;
        private readonly ServiceFactory serviceFactory;
        private readonly object lockObject = new object();

        public SingletonLifetime(ServiceFactory serviceFactory)
        {
            this.serviceFactory = serviceFactory;
        }

        public object GetInstance(IProviderOfServices serviceProvider)
        {
            if (this.instance == null)
            {
                lock (this.lockObject)
                {
                    if (this.instance == null)
                    {
                        this.instance = this.serviceFactory(serviceProvider);
                    }
                }
            }
            return this.instance;
        }
    }
}
