/*
 *     Copyright 2016 IBM Corp.
 *     Licensed under the Apache License, Version 2.0 (the "License");
 *     you may not use this file except in compliance with the License.
 *     You may obtain a copy of the License at
 *     http://www.apache.org/licenses/LICENSE-2.0
 *     Unless required by applicable law or agreed to in writing, software
 *     distributed under the License is distributed on an "AS IS" BASIS,
 *     WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 *     See the License for the specific language governing permissions and
 *     limitations under the License.
 */

using Newtonsoft.Json.Linq;
using System.Collections.Generic;

namespace JSONStoreWin8Lib.JSONStore
{
    public class JSONStoreCollection
    {
        public string collectionName { get; set; }
        public IDictionary<string, string> searchFields { get; set; }
        public IDictionary<string, string> additionalSearchFields { get; set; }
        public bool dropFirst { get; set; }
        public bool wasReopened { get; set; }

        /**
         * Creates new JSONStoreCollection object for the collection with the given name.
         * @param name="collectionName"
         * @param name="additionalSearchFields" 
         * @param name="searchFields"
         * @param name="dropFirst"
         */
        public JSONStoreCollection(string collectionName, IDictionary<string, string> searchFields, IDictionary<string, string> additionalSearchFields, bool dropFirst)
        {
            this.collectionName = collectionName;
            this.searchFields = searchFields;
            this.additionalSearchFields = additionalSearchFields;
            this.dropFirst = dropFirst;
        }

        /**
         * Permanently deletes all the documents stored in a collection and removes the accessor for that collection.
         */
        public bool removeCollection() 
        {
            // cannot remove if transaction is in progress, throw exception
            if (JSONStore.transactionInProgress) 
            {
                //JSONStoreLoggerError(@"Error: JSON_STORE_TRANSACTION_FAILURE_DURING_REMOVE_COLLECTION, code: %d", rc);
                throw new JSONStoreException(JSONStoreConstants.JSON_STORE_TRANSACTION_FAILURE_DURING_REMOVE_COLLECTION);
            }

            JSONStoreSQLLite store = JSONStoreSQLLite.sharedManager();

            if (store == null) 
            {
                //JSONStoreLoggerError(@"Error: JSON_STORE_DATABASE_NOT_OPEN, code: %d", rc);
                throw new JSONStoreException(JSONStoreConstants.JSON_STORE_DATABASE_NOT_OPEN);
            }
    
            // drop the table
            lock (JSONStore.lockThis)
            {
                bool worked = store.dropTable(collectionName);

                if (!worked)
                {
                    //JSONStoreLoggerError(@"Error: JSON_STORE_ERROR_CLEARING_COLLECTION, code: %d, collection name: %@, accessor username: %@", rc, self.collectionName, accessor != nil ? accessor.username : @"nil");
                    throw new JSONStoreException(JSONStoreConstants.JSON_STORE_ERROR_CLEARING_COLLECTION);

                }
                else
                {
                    JSONStore.removeAccessor(collectionName);
                }

                return worked;
            }
        }

        /**
         * Permanently deletes all the documents stored in a collection while preserving the accessor for the collection.
         */
        public bool clearCollection() {
            // get the shared manager
            JSONStoreSQLLite store = JSONStoreSQLLite.sharedManager();

            if (store == null)
            {
                //JSONStoreLoggerError(@"Error: JSON_STORE_DATABASE_NOT_OPEN, code: %d", rc);
                throw new JSONStoreException(JSONStoreConstants.JSON_STORE_DATABASE_NOT_OPEN);
            }

            lock (JSONStore.lockThis)
            {
                bool worked = store.clearTable(collectionName);

                if (!worked)
                {
                    // JSONStoreLoggerError(@"Error: JSON_STORE_ERROR_CLEARING_COLLECTION, code: %d, collection name: %@, accessor username: %@", rc, self.collectionName, accessor != nil ? accessor.username : @"nil");
                    throw new JSONStoreException(JSONStoreConstants.JSON_STORE_ERROR_CLEARING_COLLECTION);
                }
                return worked;
            }
        }

        /**
         * Stores data as documents in the collection.
         */
        public int addData(JArray jsonArray, bool markDirty, JSONStoreAddOptions options) 
        {
            // get the shared manager
            JSONStoreSQLLite store = JSONStoreSQLLite.sharedManager();
    
            if (store == null) {
                //JSONStoreLoggerError(@"Error: JSON_STORE_DATABASE_NOT_OPEN, code: %d", rc);
                throw new JSONStoreException(JSONStoreConstants.JSON_STORE_DATABASE_NOT_OPEN);
            }

            int numWorked = 0;

            lock (JSONStore.lockThis)
            {
                //Start transaction if no user transaction is already open
                if (!JSONStore.transactionInProgress)
                {
                    store.startTransaction();
                }

                foreach (JObject data in jsonArray)
                {
                    // loop through all the data in the array and attempt to store
                    if (store.storeObject(data, collectionName, markDirty, options.additionalSearchFields))
                    {
                        numWorked++;
                    }
                    else
                    {
                        //If we can't store all the data, we rollback and go
                        //to the error callback
                        if (!JSONStore.transactionInProgress)
                        {
                            store.rollbackTransaction();
                        }
                        throw new JSONStoreException(JSONStoreConstants.JSON_STORE_PERSISTENT_STORE_FAILURE);
                    }
                }

                // things worked, commit
                if (!JSONStore.transactionInProgress)
                {
                    store.commitTransaction();
                }
                return numWorked;
            }
        }

        /**
         * Stores data as documents in the collection.
         */
        public int remove(JArray documents, JSONStoreRemoveOptions options) {
            // get the shared manager
            JSONStoreSQLLite store = JSONStoreSQLLite.sharedManager();

            if (store == null)
            {
                //JSONStoreLoggerError(@"Error: JSON_STORE_DATABASE_NOT_OPEN, code: %d", rc);
                throw new JSONStoreException(JSONStoreConstants.JSON_STORE_DATABASE_NOT_OPEN);
            }
            
            int numUpdated = 0;
            JArray failures = new JArray();

            // loop through each document to remove
            lock (JSONStore.lockThis)
            {
                foreach (JToken document in documents)
                {
                    int lastUpdatedNum = 0;
                    lastUpdatedNum = store.remove(document, collectionName, !options.isErase, options.exact);
                    if (lastUpdatedNum < 0)
                    {
                        // store the failure
                        failures.Add(document);
                    }
                    else
                    {
                        // sucess, update the count
                        numUpdated += lastUpdatedNum;
                    }
                }

                // if failures, return each
                if (failures.Count > 0)
                {
                    //JSONStoreLoggerError(@"Error: JSON_STORE_REMOVE_WITH_QUERIES_FAILURE, code: %d, collection name: %@, accessor username: %@, failures count: %d, query failures: %@", rc, self.collectionName, accessor != nil ? accessor.username : @"nil", [failures count], failures);
                    throw new JSONStoreException(JSONStoreConstants.JSON_STORE_REMOVE_WITH_QUERIES_FAILURE, failures);

                }
                return numUpdated;
            }
        }

        /**
         * Replaces documents in the collection with given documents. This method is used to modify documents inside a collection by replacing existing documents with given documents. The field used to perform the replacement is the document unique identifier (_id).
         */
        public int replaceDocuments(JArray documents, bool markDirty)
        {
            // get the shared manager
            JSONStoreSQLLite store = JSONStoreSQLLite.sharedManager();

            if (store == null)
            {
                //JSONStoreLoggerError(@"Error: JSON_STORE_DATABASE_NOT_OPEN, code: %d", rc);
                throw new JSONStoreException(JSONStoreConstants.JSON_STORE_DATABASE_NOT_OPEN);
            }

            int numReplaced = 0;
            JArray failures = new JArray();

            lock (JSONStore.lockThis)
            {
                if (!JSONStore.transactionInProgress)
                {
                    //Start transaction
                    store.startTransaction();
                }

                foreach (JToken document in documents)
                {
                    bool worked = store.replace(document, collectionName, markDirty);

                    if (worked)
                    {
                        //It worked, increment the number of docs replaced
                        numReplaced++;
                    }
                    else
                    {
                        //If we can't store all the data, we rollback and go
                        //to the error callback
                        numReplaced = JSONStoreConstants.JSON_STORE_PERSISTENT_STORE_FAILURE;
                        failures.Add(document);
                        break;
                    }
                }

                if (numReplaced < 0)
                {
                    if (!JSONStore.transactionInProgress)
                    {
                        store.rollbackTransaction();
                    }

                    throw new JSONStoreException(JSONStoreConstants.JSON_STORE_REPLACE_DOCUMENTS_FAILURE, failures);
                }

                // things worked, commit
                if (!JSONStore.transactionInProgress)
                {
                    store.commitTransaction();
                }

                return numReplaced;
            }
        }

        /**
         * Locates documents inside a collection that matches the query.
         */
        public JArray findWithQueries(JArray queries, JSONStoreQueryOptions options)
        {

            if (options.offset > 0 && options.limit <= 0) 
            {
                //JSONStoreLoggerError(@"Error: JSON_STORE_INVALID_OFFSET, code: %d", rc);
                throw new JSONStoreException(JSONStoreConstants.JSON_STORE_INVALID_OFFSET);
            }
    
            JSONStoreSQLLite store = JSONStoreSQLLite.sharedManager();
    
            if (store == null) 
            {
                throw new JSONStoreException(JSONStoreConstants.JSON_STORE_DATABASE_NOT_OPEN);
            }

            List<string> ids = new List<string>();
            List<string> jObjectStrings = new List<string>();
            JArray fullResults = new JArray();

            lock (JSONStore.lockThis)
            {
                foreach (JToken currentQuery in queries)
                {
                    JArray results = store.find(currentQuery, collectionName, options);

                    if (results != null)
                    {
                        foreach (JObject result in results)
                        {
                            JToken value;
                            if (!result.TryGetValue("_id", out value))
                            {
                                if (!jObjectStrings.Contains(result.ToString()))
                                {
                                    fullResults.Add(result);
                                    jObjectStrings.Add(result.ToString());
                                }
                            }
                            else if (!ids.Contains(value.ToString()))
                            {
                                fullResults.Add(result);
                                ids.Add(value.ToString());
                            }
                        }

                    }
                    else
                    {

                        fullResults = null;

                        //A find failed, break and go to the error callback
                        break;
                    }
                }

                // If we get back a nil array, then the SQL statment was invalid or the database was closed.
                // If we just didn't find anything for a valid SQL statement, then we get back a non-nil empty array.
                if (fullResults == null)
                {
                    //JSONStoreLoggerError(@"Error: JSON_STORE_INVALID_SEARCH_FIELD, code: %d, collection name: %@, accessor username: %@, currentQuery: %@, JSONStoreQueryOptions: %@", rc, self.collectionName, accessor != nil ? accessor.username : @"nil", lastQuery, options);
                    throw new JSONStoreException(JSONStoreConstants.JSON_STORE_INVALID_SEARCH_FIELD);
                }

                return fullResults;
            }
        }

        
        /**
         * Locates documents inside a collection that matches the ids.
         */
        public JArray findWithIds(JArray ids)
        {
            JSONStoreQueryOptions options = new JSONStoreQueryOptions();
            options.exact = true;
            return findWithQueries(ids, options);
        }

        public JArray findWithAdvancedQuery(List<JSONStoreQuery> queryParts, JSONStoreQueryOptions options)
        {
            // the results
            JArray results = null;

            // get the shared store
            JSONStoreSQLLite store = JSONStoreSQLLite.sharedManager();
    
            if (store == null) 
            {
                //JSONStoreLoggerError(@"Error: JSON_STORE_DATABASE_NOT_OPEN, code: %d", rc);
                throw new JSONStoreException(JSONStoreConstants.JSON_STORE_DATABASE_NOT_OPEN);
            }

            lock (JSONStore.lockThis)
            {
                results = store.findWithQueryParts(queryParts, collectionName, options);

                if (results == null)
                {
                    // JSONStoreLoggerError(@"Error: JSON_STORE_INVALID_SEARCH_FIELD, code: %d, collection name: %@, accessor username: %@, currentQuery: %@, JSONStoreQueryOptions: %@", rc, self.collectionName, accessor != nil ? accessor.username : @"nil", nil, options);

                    throw new JSONStoreException(JSONStoreConstants.JSON_STORE_INVALID_SEARCH_FIELD);
                }

                return results;
            }
        }

        /**
         * Counts all dirty documents in the collection.
         */
        public int countAllDirtyDocumentsWithError()
        {
            return 0;
        }

        /**
         * Returns the total number of documents that match the query.
         */
        public int countDocuments(JObject query, bool exactMatch) {
            JSONStoreSQLLite store = JSONStoreSQLLite.sharedManager();
    
            if (store == null) {
                throw new JSONStoreException(JSONStoreConstants.JSON_STORE_DATABASE_NOT_OPEN);
            }
    
            int countResult = 0;

            lock (JSONStore.lockThis)
            {
                if (query != null && query.HasValues)
                {
                    countResult = store.countWithQuery(query, collectionName, exactMatch);
                }
                else
                {
                    countResult = store.count(collectionName);
                }

                return countResult;
            }
        }

        public int countAllDirtyDocuments()
        {
            JSONStoreSQLLite store = JSONStoreSQLLite.sharedManager();
    
            if (store == null) {
                throw new JSONStoreException(JSONStoreConstants.JSON_STORE_DATABASE_NOT_OPEN);
            }

            lock (JSONStore.lockThis)
            {
                int countResult = store.dirtyCount(collectionName);

                return countResult;
            }
        }

        /**
         * Returns whether the state of the document is dirty or not.
         */
        public bool isDirtyDocument(string documentId)
        {
            JSONStoreSQLLite store = JSONStoreSQLLite.sharedManager();

            if (store == null)
            {
                throw new JSONStoreException(JSONStoreConstants.JSON_STORE_DATABASE_NOT_OPEN);
            }

            lock (JSONStore.lockThis)
            {
                return store.isDirty(documentId, collectionName);
            }
        }

        /**
         * Get all dirty documents in the collection from the given document array.
         */
        public JArray allDirtyWithDocuments(JArray documents)
        {
            JSONStoreSQLLite store = JSONStoreSQLLite.sharedManager();

            if (store == null)
            {
                throw new JSONStoreException(JSONStoreConstants.JSON_STORE_DATABASE_NOT_OPEN);
            }

    
            JArray docsToReturn = new JArray();

            lock (JSONStore.lockThis)
            {
                JArray retArr = store.allDirtyInCollection(collectionName);

                if (retArr.Count > 0)
                {

                    foreach (JObject md in retArr)
                    {

                        //If we are passed an array of docs from pushSelected, we
                        //only want to return those that are actually dirty in the database.
                        if (documents.Count > 0)
                        {

                            string dirtyId = md.GetValue(JSONStoreConstants.JSON_STORE_FIELD_ID).ToString();
                            foreach (JObject doc in documents)
                            {
                                if (doc.GetValue(JSONStoreConstants.JSON_STORE_FIELD_ID).ToString().Equals(dirtyId))
                                {
                                    docsToReturn.Add(md);
                                    break;
                                }
                            }

                        }
                        else
                        {
                            docsToReturn.Add(md);
                        }

                        //[JSONStoreCollection _changeJSONBlobToDictionaryWithDictionary:md];
                    }
                }

                return docsToReturn;
            }
        }

        /**
         * Updates the dirty state of documents passed to clean. If the document was marked for removal it is permanently deleted.
         */
        public int markDocumentsClean(JArray documents)
        {
            JSONStoreSQLLite store = JSONStoreSQLLite.sharedManager();

            if (store == null)
            {
                throw new JSONStoreException(JSONStoreConstants.JSON_STORE_DATABASE_NOT_OPEN);
            }

    
            int numWorked = 0;
    
            JArray failedDocs = new JArray();

            lock (JSONStore.lockThis)
            {
                foreach (JObject doc in documents)
                {
                    string docId = doc.GetValue(JSONStoreConstants.JSON_STORE_FIELD_ID).ToString();
                    string operation = doc.GetValue(JSONStoreConstants.JSON_STORE_FIELD_OPERATION).ToString();

                    bool worked = store.markClean(docId, collectionName, operation);

                    if (worked)
                    {
                        numWorked++;
                    }
                    else
                    {
                        failedDocs.Add(doc);
                    }
                }

                if (failedDocs.Count > 0)
                {
                    //JSONStoreLoggerError(@"Error: JSON_STORE_COULD_NOT_MARK_DOCUMENT_PUSHED, code: %d, collection name: %@, accessor username: %@, failedDocs count: %d, failedDocs: %@, numWorked: %d", JSON_STORE_COULD_NOT_MARK_DOCUMENT_PUSHED, self.collectionName, accessor != nil ? accessor.username : @"nil", [failedDocs count], failedDocs, numWorked);
                    throw new JSONStoreException(JSONStoreConstants.JSON_STORE_COULD_NOT_MARK_DOCUMENT_PUSHED, failedDocs);
                }

                return numWorked;
            }
        }

        public int changeData(JArray dataArray, JSONStoreReplaceOptions options)
        {
            int numUpdatedOrAdded = 0;

            JSONStoreSQLLite store = JSONStoreSQLLite.sharedManager();

            if (store == null)
            {
                //JSONStoreLoggerError(@"Error: JSON_STORE_DATABASE_NOT_OPEN, code: %d", rc);
                throw new JSONStoreException(JSONStoreConstants.JSON_STORE_DATABASE_NOT_OPEN);
            }

            foreach(JObject data in dataArray) {
                JArray query = new JArray();
                
                if(options.replaceCriteria != null) {
                    foreach(string sf in options.replaceCriteria) {
                        JToken value;
                        if(data.TryGetValue(sf, out value)) {
                            JObject queryObject = new JObject();
                            queryObject.Add(sf, value);
                            query.Add(queryObject);
                        }
                    }
    
                }
                
                JArray results = null;
                if(query.Count > 0 ) {
                    JSONStoreQueryOptions queryOptions = new JSONStoreQueryOptions();
                    queryOptions.exact = true;
                    results = findWithQueries(query, queryOptions);
                }

                if(results != null && results.Count > 0) {
                    JArray docsToReplace = new JArray();

                    foreach(JObject obj in results) {
                        JObject doc = new JObject();
                        doc.Add(JSONStoreConstants.JSON_STORE_FIELD_ID, obj.GetValue(JSONStoreConstants.JSON_STORE_FIELD_ID));
                        doc.Add(JSONStoreConstants.JSON_STORE_FIELD_JSON, data);
                        docsToReplace.Add(doc);
                    }

                    int numReplaced = replaceDocuments(docsToReplace, options.markDirty);
                    if(numReplaced > 0) {
                        numUpdatedOrAdded += numReplaced;
                    }
                } else {
                    if(options.addNew) {
                        JArray addArray = new JArray();
                        addArray.Add(data);
                        int numAdded = addData(addArray, options.markDirty, new JSONStoreAddOptions());
                        numUpdatedOrAdded += numAdded;
                    }
                }
            }
    
            return numUpdatedOrAdded;
    
        }
    }
}