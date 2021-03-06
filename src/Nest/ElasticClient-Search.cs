﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Elasticsearch.Net;
using Nest.Resolvers;
using Newtonsoft.Json;

namespace Nest
{
	public partial class ElasticClient
	{
		/// <inheritdoc />
		public IQueryResponse<T> Search<T>(Func<SearchDescriptor<T>, SearchDescriptor<T>> searchSelector) where T : class
		{
			return this.Search<T, T>(searchSelector);
		}

		/// <inheritdoc />
		public IQueryResponse<TResult> Search<T, TResult>(Func<SearchDescriptor<T>, SearchDescriptor<T>> searchSelector)
			where T : class
			where TResult : class
		{
			searchSelector.ThrowIfNull("searchSelector");
			var descriptor = searchSelector(new SearchDescriptor<T>());
			var pathInfo =
				((IPathInfo<SearchQueryString>) descriptor).ToPathInfo(_connectionSettings);
			var deserializationState = this.CreateCovariantSearchSelector<T, TResult>(descriptor);
			var status = this.RawDispatch.SearchDispatch<QueryResponse<TResult>>(pathInfo,
				descriptor, deserializationState);
			return status.Success ? status.Response : CreateInvalidInstance<QueryResponse<TResult>>(status);
		}


		/// <inheritdoc />
		public Task<IQueryResponse<T>> SearchAsync<T>(Func<SearchDescriptor<T>, SearchDescriptor<T>> searchSelector)
			where T : class
		{
			return this.SearchAsync<T, T>(searchSelector);
		}

		/// <inheritdoc />
		public Task<IQueryResponse<TResult>> SearchAsync<T, TResult>(
			Func<SearchDescriptor<T>, SearchDescriptor<T>> searchSelector)
			where T : class
			where TResult : class
		{
			searchSelector.ThrowIfNull("searchSelector");
			var descriptor = searchSelector(new SearchDescriptor<T>());
			var pathInfo =
				((IPathInfo<SearchQueryString>) descriptor).ToPathInfo(_connectionSettings);
			var deserializationState = this.CreateCovariantSearchSelector<T, TResult>(descriptor);
			return this.RawDispatch.SearchDispatchAsync<QueryResponse<TResult>>(pathInfo, descriptor, deserializationState)
				.ContinueWith<IQueryResponse<TResult>>(t => t.Result.Success
					? t.Result.Response
					: CreateInvalidInstance<QueryResponse<TResult>>(t.Result));
		}

		private JsonConverter CreateCovariantSearchSelector<T, TResult>(SearchDescriptor<T> originalSearchDescriptor)
			where T : class
			where TResult : class
		{
			var types =
				(originalSearchDescriptor._Types ?? Enumerable.Empty<TypeNameMarker>()).Where(t => t.Type != null);
			if (originalSearchDescriptor._ConcreteTypeSelector == null && types.Any(t => t.Type != typeof (TResult)))
			{
				var typeDictionary = types.ToDictionary(Infer.TypeName, t => t.Type);
				originalSearchDescriptor._ConcreteTypeSelector = (o, h) =>
				{
					Type t;
					return !typeDictionary.TryGetValue(h.Type, out t) ? typeof (TResult) : t;
				};
			}
			return originalSearchDescriptor._ConcreteTypeSelector == null 
				? null 
				: new ConcreteTypeConverter<TResult>(originalSearchDescriptor._ConcreteTypeSelector);
		}
	}
}