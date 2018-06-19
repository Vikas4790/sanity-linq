﻿
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using Oslofjord.Sanity.Linq.DTOs;
using Oslofjord.Sanity.Linq.CommonTypes;
using Oslofjord.Sanity.Linq.Mutations;

namespace Oslofjord.Sanity.Linq
{
    /// <summary>
    /// Linq-to-Sanity Data Context.
    /// Handles intialization of SanityDbSets defined in inherited classes.
    /// </summary>
    public class SanityDataContext
    {

        private object _dsLock = new object();
        private ConcurrentDictionary<Type, SanityDocumentSet> _documentSets = new ConcurrentDictionary<Type, SanityDocumentSet>();

        internal bool IsShared { get; }

        public SanityClient Client { get; }

        public SanityMutationBuilder Mutations { get; }

        public JsonSerializerSettings SerializerSettings { get; }

        /// <summary>
        /// Create a new SanityDbContext using the specified options.
        /// </summary>
        /// <param name="options"></param>
        /// <param name="isShared">Indicates that the context can be used by multiple SanityDocumentSets</param>
        public SanityDataContext(SanityOptions options, JsonSerializerSettings serializerSettings = null)
        {
            if (options == null)
            {
                throw new ArgumentNullException(nameof(options));
            }
            SerializerSettings = serializerSettings ?? new JsonSerializerSettings
            {
                ContractResolver = new CamelCasePropertyNamesContractResolver(),
                NullValueHandling = NullValueHandling.Ignore,
                Converters = new List<JsonConverter> { new SanityReferenceTypeConverter() }
            };
            Client = new SanityClient(options, serializerSettings);
            Mutations = new SanityMutationBuilder(Client);
        }

       
        /// <summary>
        /// Create a new SanityDbContext using the specified options.
        /// </summary>
        /// <param name="options"></param>
        /// <param name="isShared">Indicates that the context can be used by multiple SanityDocumentSets</param>
        internal SanityDataContext(SanityOptions options, bool isShared) : this(options)
        {
            IsShared = isShared;
        }
             

        /// <summary>
        /// Returns an IQueryable document set for specified type
        /// </summary>
        /// <typeparam name="TDoc"></typeparam>
        /// <returns></returns>
        public virtual SanityDocumentSet<TDoc> DocumentSet<TDoc>()
        {
            lock (_dsLock)
            {
                if (!_documentSets.ContainsKey(typeof(TDoc)))
                {
                    _documentSets[typeof(TDoc)] = new SanityDocumentSet<TDoc>(this);
                }
            }
            return _documentSets[(typeof(TDoc))] as SanityDocumentSet<TDoc>;
        }

        public virtual SanityDocumentSet<SanityImageAsset> Images => DocumentSet<SanityImageAsset>();

        public virtual SanityDocumentSet<SanityFileAsset> Files => DocumentSet<SanityFileAsset>();

        public virtual SanityDocumentSet<SanityDocument> Documents => DocumentSet<SanityDocument>();

        public virtual void ClearChanges()
        {
            Mutations.Clear();
        }

        /// <summary>
        /// Sends all changes registered on Document sets to Sanity as a transactional set of mutations.
        /// </summary>
        /// <param name="returnIds"></param>
        /// <param name="returnDocuments"></param>
        /// <param name="visibility"></param>
        /// <returns></returns>
        public async Task<SanityMutationResponse> CommitAsync(bool returnIds = false, bool returnDocuments = false, SanityMutationVisibility visibility = SanityMutationVisibility.Sync)
        {
            var result = await Client.CommitMutationsAsync(Mutations.Build(Client.SerializerSettings), returnIds, returnDocuments, visibility).ConfigureAwait(false);
            Mutations.Clear();
            return result;
        }

        /// <summary>
        /// Sends all changes registered on document sets of specified type to Sanity as a transactional set of mutations.
        /// </summary>
        /// <param name="returnIds"></param>
        /// <param name="returnDocuments"></param>
        /// <param name="visibility"></param>
        /// <returns></returns>
        public async Task<SanityMutationResponse<TDoc>> CommitAsync<TDoc>(bool returnIds = false, bool returnDocuments = false, SanityMutationVisibility visibility = SanityMutationVisibility.Sync)
        {
            var mutations = Mutations.For<TDoc>();
            if (mutations.Mutations.Count > 0)
            {
                var result = await Client.CommitMutationsAsync<TDoc>(mutations.Build(), returnIds, returnDocuments, visibility).ConfigureAwait(false);
                mutations.Clear();
                return result;
            }
            throw new Exception($"No pending changes for document type {typeof(TDoc)}");
        }

    }
}