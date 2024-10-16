﻿using System;
using System.Collections.Generic;
using System.Linq;

namespace Cornflakes
{
    internal class ServiceCreationResolver
    {
        Dictionary<Type, Type> creators = new Dictionary<Type, Type>();

        public void RegisterServiceCreator<TAttribute, TCreator>() 
            where TAttribute : Attribute 
            where TCreator : ICreationStrategy
        {
            this.creators[typeof(TAttribute)] = typeof(TCreator);
        }

        public ICreationStrategy GetServiceCreator(Type implementationType)
        {
            Attribute attribute = implementationType.GetCustomAttributes(true)
                .OfType<Attribute>()
                .First(attr => this.creators.ContainsKey(attr.GetType()));

            if (attribute == null)
            {
                return new TransientCreator();
            }

            return (ICreationStrategy)Activator.CreateInstance(this.creators[attribute.GetType()]);
        }
    }
}
