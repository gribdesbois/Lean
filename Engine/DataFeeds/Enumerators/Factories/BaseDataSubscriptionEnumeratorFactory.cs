﻿/*
 * QUANTCONNECT.COM - Democratizing Finance, Empowering Individuals.
 * Lean Algorithmic Trading Engine v2.0. Copyright 2014 QuantConnect Corporation.
 * 
 * Licensed under the Apache License, Version 2.0 (the "License"); 
 * you may not use this file except in compliance with the License.
 * You may obtain a copy of the License at http://www.apache.org/licenses/LICENSE-2.0
 * 
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
 *
*/

using System;
using System.Collections.Generic;
using System.Linq;
using QuantConnect.Data;
using QuantConnect.Data.UniverseSelection;
using QuantConnect.Data.Auxiliary;
using QuantConnect.Interfaces;

namespace QuantConnect.Lean.Engine.DataFeeds.Enumerators.Factories
{
    /// <summary>
    /// Provides a default implementation of <see cref="ISubscriptionEnumeratorFactory"/> that uses
    /// <see cref="BaseData"/> factory methods for reading sources
    /// </summary>
    public class BaseDataSubscriptionEnumeratorFactory : ISubscriptionEnumeratorFactory
    {
        private readonly Func<SubscriptionRequest, IEnumerable<DateTime>> _tradableDaysProvider;
        private readonly MapFileResolver _mapFileResolver;
        private readonly IFactorFileProvider _factorFileProvider;
        private readonly IDataFileCacheProvider _dataFileCacheProvider;

        /// <summary>
        /// Initializes a new instance of the <see cref="BaseDataSubscriptionEnumeratorFactory"/> class
        /// </summary>
        /// <param name="mapFileResolver">Map file Resolver</param>
        /// <param name="factorFileProvider">Factor file provider</param>
        /// <param name="dataFileCacheProvider">Data cache provider</param>
        /// <param name="tradableDaysProvider">Function used to provide the tradable dates to be enumerator. Specify null to default to <see cref="SubscriptionRequest.TradableDays"/></param>
        public BaseDataSubscriptionEnumeratorFactory(MapFileResolver mapFileResolver, IFactorFileProvider factorFileProvider,
                                                     IDataFileCacheProvider dataFileCacheProvider, 
                                                     Func<SubscriptionRequest, IEnumerable<DateTime>> tradableDaysProvider = null)
        {
            _dataFileCacheProvider = dataFileCacheProvider;
            _tradableDaysProvider = tradableDaysProvider ?? (request => request.TradableDays);
            _mapFileResolver = mapFileResolver;
            _factorFileProvider = factorFileProvider;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="BaseDataSubscriptionEnumeratorFactory"/> class
        /// </summary>
        /// <param name="dataFileCacheProvider">Data cache provider</param>
        /// <param name="tradableDaysProvider">Function used to provide the tradable dates to be enumerator.
        /// Specify null to default to <see cref="SubscriptionRequest.TradableDays"/></param>
        public BaseDataSubscriptionEnumeratorFactory(IDataFileCacheProvider dataFileCacheProvider, Func<SubscriptionRequest, IEnumerable<DateTime>> tradableDaysProvider = null)
        {
            _dataFileCacheProvider = dataFileCacheProvider;
            _tradableDaysProvider = tradableDaysProvider ?? (request => request.TradableDays);
        }

        /// <summary>
        /// Creates an enumerator to read the specified request
        /// </summary>
        /// <param name="request">The subscription request to be read</param>
        /// <param name="dataFileProvider">Provider used to get data when it is not present on disk</param>
        /// <returns>An enumerator reading the subscription request</returns>
        public IEnumerator<BaseData> CreateEnumerator(SubscriptionRequest request, IDataFileProvider dataFileProvider)
        {
            var sourceFactory = (BaseData)Activator.CreateInstance(request.Configuration.Type);

            foreach (var date in _tradableDaysProvider(request))
            {
                var currentSymbol = request.Configuration.MappedSymbol;
                request.Configuration.MappedSymbol = GetMappedSymbol(request, date);
                var source = sourceFactory.GetSource(request.Configuration, date, false);
                request.Configuration.MappedSymbol = currentSymbol;
                var factory = SubscriptionDataSourceReader.ForSource(source, dataFileProvider, _dataFileCacheProvider, request.Configuration, date, false);
                var entriesForDate = factory.Read(source);
                foreach(var entry in entriesForDate)
                {
                    yield return entry;
                }
            }
        }
        private string GetMappedSymbol(SubscriptionRequest request, DateTime date)
        {
            var config = request.Configuration;
            if (config.Symbol.ID.SecurityType == SecurityType.Option ||
                config.Symbol.ID.SecurityType == SecurityType.Equity )
            {
                var mapFile = config.Symbol.HasUnderlying ?
                        _mapFileResolver.ResolveMapFile(config.Symbol.Underlying.ID.Symbol, config.Symbol.Underlying.ID.Date) :
                        _mapFileResolver.ResolveMapFile(config.Symbol.ID.Symbol, config.Symbol.ID.Date);

                return mapFile.GetMappedSymbol(date);
            }
            else
            {
                return config.MappedSymbol;
            }
        }
    }
}
