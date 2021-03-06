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

using JSONStoreWin8Lib.JSONStore;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
using Windows.Foundation;

namespace JSONStoreWin8

{
    public sealed class StoragePlugin
    {
        /**
         * Creates the datababase table based on the json schema passed in
         */
        public IAsyncOperation<StorageResult> provision(string parameters)
        {
            return AsyncInfo.Run((token) =>
               Task.Run<StorageResult>(() =>
               {
                   try
                   {
                       // deserialize the parameters passed in as an array of strings
                       string[] paramStrings = JsonConvert.DeserializeObject<string[]>(parameters);

                       // the first parameter is the collection name
                       string collectionName = paramStrings[0];

                       // the second parameter is a JSON object of serach fields, needs further parsing into a Dictionary
                       IDictionary<string, string> searchFields = JsonConvert.DeserializeObject<Dictionary<string, string>>(paramStrings[1]);

                       // the third paramter is a JSON object of options, needs further parsing into JSONStoreProvisionOptions object
                       JSONStoreProvisionOptions storeOptions = JsonConvert.DeserializeObject<JSONStoreProvisionOptions>(paramStrings[2]);
                    
                       // create a new JSONStoreCollection object
                       JSONStoreCollection[] collections = { new JSONStoreCollection(collectionName, searchFields, storeOptions.additionalSearchFields, storeOptions.dropCollection) };

                       // create/open the collection
                       JSONStore.openCollections(collections, storeOptions);

                       // determine the return code
                       int rc = collections[0].wasReopened ? JSONStoreConstants.JSON_STORE_PROVISION_TABLE_EXISTS : JSONStoreConstants.JSON_STORE_RC_OK;

                       // pass the return code back to the JavaScript layer
                       return new StorageResult(Status.OK, rc);
                   }
                   catch (JSONStoreException jsonException)
                   {
                       // catch a JSONStore specific exception and return the error code
                       return new StorageResult(Status.ERROR, jsonException.errorCode);
                   }
                   catch (Exception)
                   {
                       // something unknown went wrong
                       //JSONStoreLoggerException(exception);
                       return new StorageResult(Status.ERROR, JSONStoreConstants.JSON_STORE_PERSISTENT_STORE_FAILURE);
                   }
               }, token));
        }

        public IAsyncOperation<StorageResult> isKeyGenRequired(string parameters)
        {
            return AsyncInfo.Run((token) =>
               Task.Run<StorageResult>(() =>
               {
                   try
                   {
                       // deserialize the parameters passed in as an array of strings
                       string[] paramStrings = JsonConvert.DeserializeObject<string[]>(parameters);
                       string username = paramStrings[0];

                       // create a security manager
                       JSONStoreSecurityManager secMgr = JSONStoreSecurityManager.sharedManager();
                       bool isStored = secMgr.isKeyStored(username);

                       // do some checking
                       return new StorageResult(Status.OK, isStored ? JSONStoreConstants.JSON_STORE_RC_JS_TRUE : JSONStoreConstants.JSON_STORE_RC_JS_FALSE);
                   }
                   catch (Exception)
                   {
                       return new StorageResult(Status.ERROR, JSONStoreConstants.JSON_STORE_PERSISTENT_STORE_FAILURE);
                   }
                   
               }, token));
        }

        /**
         * Takes user data in the form of a string of JSON and stores it in the database. 
         * If the JSON document is an array, we will store each of the top level objects
         * in the array as individual documents
         */
        public IAsyncOperation<StorageResult> store(string parameters)
        {
            return AsyncInfo.Run((token) =>
               Task.Run<StorageResult>(() =>
               {
                   try 
                   {
                       // deserialize the parameters passed in as an array of strings
                       string[] paramStrings = JsonConvert.DeserializeObject<string[]>(parameters);

                       // the first parameter is the collection name
                       string collectionName = paramStrings[0];

                       // the second parameter is the data to be added to the collection
                       JArray dataArray = JArray.Parse(paramStrings[1]);

                       // the third parameter is a JSON object of options, needs further parsing into a JSONStoreAddOptionsobject
                       JSONStoreAddOptions storeOptions = JsonConvert.DeserializeObject<JSONStoreAddOptions>(paramStrings[2]);

                       // get the JSONStoreCollection to add the new data to
                       JSONStoreCollection collection = JSONStore.getCollectionWithName(collectionName);

                       if (collection == null) 
                       {
                           // Collection was not opened, throw error
                           return new StorageResult(Status.ERROR, JSONStoreConstants.JSON_STORE_DATABASE_NOT_OPEN);
                       } 
                       else 
                       {
                           // store the data
                           int docsStored = collection.addData(dataArray, storeOptions.isAdd, storeOptions);

                           // return the number of docs stored
                           return new StorageResult(Status.OK, docsStored);
                       }
                   }
                   catch (JSONStoreException jsonException)
                   {
                       // catch a JSONStore specific exception and return the error code
                       return new StorageResult(Status.ERROR, jsonException.errorCode);
                   }
                   catch(Exception) 
                   {
                       // something unknown went wrong
                       return new StorageResult(Status.ERROR, JSONStoreConstants.JSON_STORE_PERSISTENT_STORE_FAILURE);
                   }
               }, token));
        }

        public IAsyncOperation<StorageResult> advancedFind(string parameters)
        {
            return AsyncInfo.Run((token) =>
               Task.Run<StorageResult>(() =>
               {
                   try
                   {
                       // deserialize the parameters passed in as an array of strings
                       string[] paramStrings = JsonConvert.DeserializeObject<string[]>(parameters);

                       // the parameter first is the collection name
                       string collectionName = paramStrings[0];

                       // the second paramter is the queries to execute
                       JArray inputQuery = JArray.Parse(paramStrings[1]);

                       // the third parameter is a JSON object of options, needs furthe prarsing into JSONStoreQueryOptions object
                       JSONStoreQueryOptions queryOptions = JsonConvert.DeserializeObject<JSONStoreQueryOptions>(paramStrings[2]);

                       // get the JSONStoreCollection to query
                       JSONStoreCollection collection = JSONStore.getCollectionWithName(collectionName);

                       if (collection == null)
                       {
                           return new StorageResult(Status.ERROR, JSONStoreConstants.JSON_STORE_DATABASE_NOT_OPEN);
                       }
                       else
                       {
                           // the individual query parts that we will pull out of the inputQuery
                           List<JSONStoreQuery> queryParts = new List<JSONStoreQuery>();

                           // loop through the query parts and store the values into a JSONStoreQuery object
                           foreach (JObject queryPart in inputQuery)
                           {
                               JSONStoreQuery query = new JSONStoreQuery();
                               foreach (JProperty part in queryPart.Children())
                               {
                                   if (part.Value.Type == JTokenType.Array)
                                   {
                                       if (part.Name.IndexOf(JSONStoreConstants.JSON_STORE_QUERY_LIKE) >= 0)
                                       {
                                           query.like = (JArray)part.Value;
                                       }
                                       else if (part.Name.IndexOf(JSONStoreConstants.JSON_STORE_QUERY_NOT_LIKE) >= 0)
                                       {
                                           query.notLike = (JArray)part.Value;
                                       }
                                       else if (part.Name.IndexOf(JSONStoreConstants.JSON_STORE_QUERY_RIGHT_LIKE) >= 0)
                                       {
                                           query.rightLike = (JArray)part.Value;
                                       }
                                       else if (part.Name.IndexOf(JSONStoreConstants.JSON_STORE_QUERY_NOT_RIGHT_LIKE) >= 0)
                                       {
                                           query.notRightLike = (JArray)part.Value;
                                       }
                                       else if (part.Name.IndexOf(JSONStoreConstants.JSON_STORE_QUERY_LEFT_LIKE) >= 0)
                                       {
                                           query.leftLike = (JArray)part.Value;
                                       }
                                       else if (part.Name.IndexOf(JSONStoreConstants.JSON_STORE_QUERY_NOT_LEFT_LIKE) >= 0)
                                       {
                                           query.notLeftLike = (JArray)part.Value;
                                       }
                                       else if (part.Name.IndexOf(JSONStoreConstants.JSON_STORE_QUERY_LESSTHAN) >= 0)
                                       {
                                           query.lessThan = (JArray)part.Value;
                                       }
                                       else if (part.Name.IndexOf(JSONStoreConstants.JSON_STORE_QUERY_LESSTHANEQUALS) >= 0)
                                       {
                                           query.lessOrEqualThan = (JArray)part.Value;
                                       }
                                       else if (part.Name.IndexOf(JSONStoreConstants.JSON_STORE_QUERY_GREATERTHAN) >= 0)
                                       {
                                           query.greaterThan = (JArray)part.Value;
                                       }
                                       else if (part.Name.IndexOf(JSONStoreConstants.JSON_STORE_QUERY_GREATERTHANEQUALS) >= 0)
                                       {
                                           query.greaterOrEqualThan = (JArray)part.Value;
                                       }
                                       else if (part.Name.IndexOf(JSONStoreConstants.JSON_STORE_QUERY_EQUALS) >= 0)
                                       {
                                           query.equal = (JArray)part.Value;
                                       }
                                       else if (part.Name.IndexOf(JSONStoreConstants.JSON_STORE_QUERY_NOT_EQUALS) >= 0)
                                       {
                                           query.notEqual = (JArray)part.Value;
                                       }
                                       else if (part.Name.IndexOf(JSONStoreConstants.JSON_STORE_QUERY_INSIDE) >= 0)
                                       {
                                           query.inside = (JArray)part.Value;
                                       }
                                       else if (part.Name.IndexOf(JSONStoreConstants.JSON_STORE_QUERY_NOT_INSIDE) >= 0)
                                       {
                                           query.notInside = (JArray)part.Value;
                                       }
                                       else if (part.Name.IndexOf(JSONStoreConstants.JSON_STORE_QUERY_BETWEEN) >= 0)
                                       {
                                           query.between = (JArray)part.Value;
                                       }
                                       else if (part.Name.IndexOf(JSONStoreConstants.JSON_STORE_QUERY_NOT_BETWEEN) >= 0)
                                       {
                                           query.notBetween = (JArray)part.Value;
                                       }

                                       queryParts.Add(query);
                                   }
                               }
                           }

                           // execute the queries
                           JArray results = collection.findWithAdvancedQuery(queryParts, queryOptions);
                           return new StorageResult(Status.OK, results);
                       }

                   }
                   catch (JSONStoreException jsonException)
                   {
                       // catch a JSONStore specific exception and return the error code
                       return new StorageResult(Status.ERROR, jsonException.errorCode);
                   }
                   catch (Exception)
                   {
                       return new StorageResult(Status.ERROR, JSONStoreConstants.JSON_STORE_PERSISTENT_STORE_FAILURE);
                   }
               }, token));
        }

        public IAsyncOperation<StorageResult> find(string parameters)
        {
            return AsyncInfo.Run((token) =>
               Task.Run<StorageResult>(() =>
               {
                    try {
                        // deserialize the parameters passed in as an array of strings
                        string[] paramStrings = JsonConvert.DeserializeObject<string[]>(parameters);

                        // the parameter first is the collection name
                        string collectionName = paramStrings[0];

                        // the second paramter is the queries to execute
                        JArray queries = JArray.Parse(paramStrings[1]);

                        // the third parameter is a JSON object of options, needs furthe prarsing into JSONStoreQueryOptions object
                        JSONStoreQueryOptions queryOptions = JsonConvert.DeserializeObject<JSONStoreQueryOptions>(paramStrings[2]);

                        // get the JSONStoreCollection to query
                        JSONStoreCollection collection = JSONStore.getCollectionWithName(collectionName);
                      
                        if (collection == null) {
                           return new StorageResult(Status.ERROR, JSONStoreConstants.JSON_STORE_DATABASE_NOT_OPEN);
                        } else {
                            // execute the queries
                            JArray results = collection.findWithQueries(queries, queryOptions);
                            return new StorageResult(Status.OK, results);
                        }

                    }
                    catch (JSONStoreException jsonException)
                    {
                        // catch a JSONStore specific exception and return the error code
                        return new StorageResult(Status.ERROR, jsonException.errorCode);
                    }
                    catch (Exception) {
                        return new StorageResult(Status.ERROR, JSONStoreConstants.JSON_STORE_PERSISTENT_STORE_FAILURE);
                    }
               }, token));
        }

        public IAsyncOperation<StorageResult> findById(string parameters)
        {
            return AsyncInfo.Run((token) =>
                Task.Run<StorageResult>(() =>
                {
                    try
                    {
                        // deserialize the parameters passed in as an array of strings
                        string[] paramStrings = JsonConvert.DeserializeObject<string[]>(parameters);

                        // the first is the collection name
                        string collectionName = paramStrings[0];

                        // the second are the ids to search on
                        JArray ids = JArray.Parse(paramStrings[1]);

                        // get the JSONStoreCollection to query
                        JSONStoreCollection collection = JSONStore.getCollectionWithName(collectionName);
                        if (collection == null)
                        {
                            return new StorageResult(Status.ERROR, JSONStoreConstants.JSON_STORE_DATABASE_NOT_OPEN);
                        }
                        else
                        {
                            JArray fullResults = new JArray();
                            foreach (JValue i in ids)
                            {
                                // the individual query parts that we will pull out of the inputQuery
                                List<JSONStoreQuery> queryParts = new List<JSONStoreQuery>();
                                JSONStoreQuery query = new JSONStoreQuery();
                                JArray idArray = new JArray();
                                idArray.Add(i);
                                query.ids = idArray;
                                queryParts.Add(query);

                                // execute the query
                                JArray results = collection.findWithAdvancedQuery(queryParts, null);
                                foreach(JToken jtoken in results) {
                                    fullResults.Add(jtoken);
                                }
                            }

                            return new StorageResult(Status.OK, fullResults);
                        }
                    }
                    catch (JSONStoreException jsonException)
                    {
                        // catch a JSONStore specific exception and return the error code
                        return new StorageResult(Status.ERROR, jsonException.errorCode);
                    }
                    catch (Exception)
                    {
                        return new StorageResult(Status.ERROR, JSONStoreConstants.JSON_STORE_PERSISTENT_STORE_FAILURE);
                    }
                }, token));
        }

        public IAsyncOperation<StorageResult> replace(string parameters)
        {
            return AsyncInfo.Run((token) =>
                Task.Run<StorageResult>(() =>
                {
                    try
                    {
                        // deserialize the parameters passed in as an array of strings
                        string[] paramStrings = JsonConvert.DeserializeObject<string[]>(parameters);

                        // the first is the collection name
                        string collectionName = paramStrings[0];

                        // the second is an array of documents to replace
                        JArray documents = JArray.Parse(paramStrings[1]);

                        // the third argument is a JSON object of options
                        JSONStoreReplaceOptions options = JsonConvert.DeserializeObject<JSONStoreReplaceOptions>(paramStrings[2]);

                        // get the collection
                        JSONStoreCollection collection = JSONStore.getCollectionWithName(collectionName);
                        if (collection == null)
                        {
                            return new StorageResult(Status.ERROR, JSONStoreConstants.JSON_STORE_DATABASE_NOT_OPEN);
                        }
                        else
                        {
                            int numReplaced = collection.replaceDocuments(documents, !options.isRefresh);
                            return new StorageResult(Status.OK, numReplaced);
                        }
                    }
                    catch (JSONStoreException jsonException)
                    {
                        if (jsonException.data != null)
                        {
                            // catch a JSONStore specific exception and return the failed update data
                            return new StorageResult(Status.ERROR, jsonException.data);
                        }
                        else
                        {
                            // catch a JSONStore specific exception and return the error code
                            return new StorageResult(Status.ERROR, jsonException.errorCode);
                        }
                    }
                    catch (Exception)
                    {
                        // unexpected error
                        return new StorageResult(Status.ERROR, JSONStoreConstants.JSON_STORE_PERSISTENT_STORE_FAILURE);
                    }
               }, token));
        }

        public IAsyncOperation<StorageResult> remove(string parameters)
        {
            return AsyncInfo.Run((token) =>
               Task.Run<StorageResult>(() =>
               {
                   try
                   {
                       // deserialize the parameters passed in as an array of strings
                       string[] paramStrings = JsonConvert.DeserializeObject<string[]>(parameters);

                       // the first is the collection name
                       string collectionName = paramStrings[0];

                       // the second is an array of documents to remove
                       JArray documents = JArray.Parse(paramStrings[1]);

                       // the third argument is a JSON object of options
                       JSONStoreRemoveOptions options = JsonConvert.DeserializeObject<JSONStoreRemoveOptions>(paramStrings[2]);

                       // get the collection
                       JSONStoreCollection collection = JSONStore.getCollectionWithName(collectionName);
                       if (collection == null)
                       {
                           return new StorageResult(Status.ERROR, JSONStoreConstants.JSON_STORE_DATABASE_NOT_OPEN);
                       }
                       else
                       {
                           int numUpdated = collection.remove(documents, options);
                           return new StorageResult(Status.OK, numUpdated);
                       }
                   }
                   catch (Exception)
                   {
                        return new StorageResult(Status.ERROR, JSONStoreConstants.JSON_STORE_PERSISTENT_STORE_FAILURE);
                   }
               }, token));
        }

        public IAsyncOperation<StorageResult> localCount(string parameters)
        {
            return AsyncInfo.Run((token) =>
               Task.Run<StorageResult>(() =>
                {
                    try {

                        // deserialize the parameters passed in as an array of strings
                        string[] paramStrings = JsonConvert.DeserializeObject<string[]>(parameters);

                        // the first is the collection name
                        string collectionName = paramStrings[0];

                        // get the JSONStoreCollection to query
                        JSONStoreCollection collection = JSONStore.getCollectionWithName(collectionName);
                        if (collection == null)
                        {
                            return new StorageResult(Status.ERROR, JSONStoreConstants.JSON_STORE_DATABASE_NOT_OPEN);
                        }
                        else {
                            int countResult = collection.countAllDirtyDocuments();
                            return new StorageResult(Status.OK, countResult);
                        }

                    }
                    catch (JSONStoreException jsonException)
                    {
                        // catch a JSONStore specific exception and return the error code
                        return new StorageResult(Status.ERROR, jsonException.errorCode);
                    }
                    catch(Exception) {
                        return new StorageResult(Status.ERROR, JSONStoreConstants.JSON_STORE_PERSISTENT_STORE_FAILURE);
                    }
                }, token));
        }

        public IAsyncOperation<StorageResult> count(string parameters)
        {
            return AsyncInfo.Run((token) =>
                Task.Run<StorageResult>(() =>
                {
                    try
                    {
                        // deserialize the parameters passed in as an array of strings
                        string[] paramStrings = JsonConvert.DeserializeObject<string[]>(parameters);

                        // the first is the collection name
                        string collectionName = paramStrings[0];

                        // the query
                        JObject query = JObject.Parse(paramStrings[1]);

                        // the third argument is a JSON object of options
                        JSONStoreQueryOptions queryOptions = JsonConvert.DeserializeObject<JSONStoreQueryOptions>(paramStrings[2]);

                        // get the JSONStoreCollection to query
                        JSONStoreCollection collection = JSONStore.getCollectionWithName(collectionName);
                        if (collection == null)
                        {
                            return new StorageResult(Status.ERROR, JSONStoreConstants.JSON_STORE_DATABASE_NOT_OPEN);
                        }
                        else
                        {
                            int countResult = collection.countDocuments(query, queryOptions.exact);
                            return new StorageResult(Status.OK, countResult);
                        }
                    }
                    catch (JSONStoreException jsonException)
                    {
                        // catch a JSONStore specific exception and return the error code
                        return new StorageResult(Status.ERROR, jsonException.errorCode);
                    }
                     catch (Exception)
                    {
                        return new StorageResult(Status.ERROR, JSONStoreConstants.JSON_STORE_PERSISTENT_STORE_FAILURE);
                    }
                }, token));
        }

        public IAsyncOperation<StorageResult> isDirty(string parameters)
        {
            return AsyncInfo.Run((token) =>
                Task.Run<StorageResult>(() =>
                {
                    try {
                        // deserialize the parameters passed in as an array of strings
                        string[] paramStrings = JsonConvert.DeserializeObject<string[]>(parameters);

                        // the first is the collection name
                        string collectionName = paramStrings[0];

                        // the query
                        JObject query = JObject.Parse(paramStrings[1]);

                        // get the JSONStoreCollection to check against
                        JSONStoreCollection collection = JSONStore.getCollectionWithName(collectionName);
                        if (collection == null)
                        {
                            return new StorageResult(Status.ERROR, JSONStoreConstants.JSON_STORE_DATABASE_NOT_OPEN);
                        }
                        else
                        {
                            JToken idToken;
                            string id;
                            if (query.TryGetValue(JSONStoreConstants.JSON_STORE_FIELD_ID, out idToken))
                            {
                                id = idToken.ToString();
                                bool result = collection.isDirtyDocument(id);
                                return new StorageResult(Status.OK, result ? JSONStoreConstants.JSON_STORE_RC_JS_TRUE : JSONStoreConstants.JSON_STORE_RC_JS_FALSE);
                            } else {
                                return new StorageResult(Status.ERROR, JSONStoreConstants.JSON_STORE_PERSISTENT_STORE_FAILURE);
                            }
                        }
                    }
                    catch (JSONStoreException jsonException)
                    {
                        // catch a JSONStore specific exception and return the error code
                        return new StorageResult(Status.ERROR, jsonException.errorCode);
                    }
                    catch(Exception) {
                         return new StorageResult(Status.ERROR, JSONStoreConstants.JSON_STORE_PERSISTENT_STORE_FAILURE);
                     }
                }, token));
        }

        public IAsyncOperation<StorageResult> allDirty(string parameters)
        {
            return AsyncInfo.Run((token) =>
                Task.Run<StorageResult>(() =>
                {
                    try
                    {
                        // deserialize the parameters passed in as an array of strings
                        string[] paramStrings = JsonConvert.DeserializeObject<string[]>(parameters);

                        // the first is the collection name
                        string collectionName = paramStrings[0];

                        // the second is an array of documents
                        JArray documents = JArray.Parse(paramStrings[1]);

                        // get the JSONStoreCollection
                        JSONStoreCollection collection = JSONStore.getCollectionWithName(collectionName);
                        if (collection == null)
                        {
                            return new StorageResult(Status.ERROR, JSONStoreConstants.JSON_STORE_DATABASE_NOT_OPEN);
                        }
                        else
                        {
                            JArray dirtyDocs = collection.allDirtyWithDocuments(documents);
                            return new StorageResult(Status.OK, dirtyDocs);
                        }
                    }
                    catch (JSONStoreException jsonException)
                    {
                        // catch a JSONStore specific exception and return the error code
                        return new StorageResult(Status.ERROR, jsonException.errorCode);
                    }
                    catch (Exception)
                    {
                        return new StorageResult(Status.ERROR, JSONStoreConstants.JSON_STORE_PERSISTENT_STORE_FAILURE);
                    }
                }, token));
        }

        public IAsyncOperation<StorageResult> markClean(string parameters)
        {
            return AsyncInfo.Run((token) =>
                Task.Run<StorageResult>(() =>
                {
                    try
                    {
                        // deserialize the parameters passed in as an array of strings
                        string[] paramStrings = JsonConvert.DeserializeObject<string[]>(parameters);
                        
                        // the first is the collection name
                        string collectionName = paramStrings[0];
                        
                        // the second is an array of documents
                        JArray documents = JArray.Parse(paramStrings[1]);

                        JSONStoreCollection collection = JSONStore.getCollectionWithName(collectionName);
                        if (collection == null)
                        {
                            return new StorageResult(Status.ERROR, JSONStoreConstants.JSON_STORE_DATABASE_NOT_OPEN);
                        }
                        else
                        {
                            int numCleaned = collection.markDocumentsClean(documents);
                            return new StorageResult(Status.OK, numCleaned);
                        }
                    }
                    catch (JSONStoreException jsonException)
                    {
                        // catch a JSONStore specific exception and return the error code
                        return new StorageResult(Status.ERROR, jsonException.errorCode);
                    }
                    catch (Exception)
                    {
                        return new StorageResult(Status.ERROR, JSONStoreConstants.JSON_STORE_PERSISTENT_STORE_FAILURE);
                    }
                }, token));
        }

        public IAsyncOperation<StorageResult> dropTable(string parameters)
        {
            return AsyncInfo.Run((token) =>
                Task.Run<StorageResult>(() =>
                {
                    try
                    {
                        // deserialize the parameters passed in as an array of strings
                        string[] paramStrings = JsonConvert.DeserializeObject<string[]>(parameters);

                        // the first is the collection name
                        string collectionName = paramStrings[0];

                        // get the JSONStoreCollection to add the new data to
                        JSONStoreCollection collection = JSONStore.getCollectionWithName(collectionName);

                        if (collection == null)
                        {
                            return new StorageResult(Status.ERROR, JSONStoreConstants.JSON_STORE_DATABASE_NOT_OPEN);
                        }
                        else
                        {
                            collection.removeCollection();
                            return new StorageResult(Status.OK, JSONStoreConstants.JSON_STORE_RC_OK);
                        }
                    }
                    catch (JSONStoreException jsonException)
                    {
                        // catch a JSONStore specific exception and return the error code
                        return new StorageResult(Status.ERROR, jsonException.errorCode);
                    }
                    catch (Exception)
                    {
                        return new StorageResult(Status.ERROR, JSONStoreConstants.JSON_STORE_PERSISTENT_STORE_FAILURE);
                    }
                }, token));
        }

        public IAsyncOperation<StorageResult> closeDatabase(string parameters)
        {
            return AsyncInfo.Run((token) =>
                Task.Run<StorageResult>(() =>
                {
                    try
                    {
                        JSONStore.closeAllCollections();
                        return new StorageResult(Status.OK, JSONStoreConstants.JSON_STORE_RC_OK);
                        
                    }
                    catch (JSONStoreException jsonException)
                    {
                        // catch a JSONStore specific exception and return the error code
                        return new StorageResult(Status.ERROR, jsonException.errorCode);
                    }
                    catch (Exception)
                    {
                        return new StorageResult(Status.ERROR, JSONStoreConstants.JSON_STORE_PERSISTENT_STORE_FAILURE);
                    }
                }, token));
        }

        public IAsyncOperation<StorageResult> changePassword(string parameters)
        {
            return AsyncInfo.Run((token) =>
                Task.Run<StorageResult>(() =>
                {
                    try
                    {
                        // deserialize the parameters passed in as an array of strings
                        string[] paramStrings = JsonConvert.DeserializeObject<string[]>(parameters);

                        // the first param is the old password
                        string oldPassword = paramStrings[0];

                        // the second param is the new password
                        string newPassword = paramStrings[1];

                        // the third param is the user name
                        string username = paramStrings[2];

                        // change the password
                        JSONStore.changeCurrentPassword(oldPassword, newPassword, username);

                        return new StorageResult(Status.OK, JSONStoreConstants.JSON_STORE_RC_OK);
                    }
                    catch (JSONStoreException jsonException)
                    {
                        // catch a JSONStore specific exception and return the error code
                        return new StorageResult(Status.ERROR, jsonException.errorCode);
                    }
                    catch (Exception)
                    {
                        return new StorageResult(Status.ERROR, JSONStoreConstants.JSON_STORE_PERSISTENT_STORE_FAILURE);
                    }
                }, token));
        }

        public IAsyncOperation<StorageResult> destroyDbFileAndKeychain(string parameters)
        {
            return AsyncInfo.Run((token) =>
                 Task.Run<StorageResult>(() =>
                 {
                    try {

                         // deserialize the parameters passed in as an array of strings
                        string[] paramStrings = JsonConvert.DeserializeObject<string[]>(parameters);

                        string username = paramStrings[0];
                        // destroy the db
                        JSONStore.destroyData(username);
                        return new StorageResult(Status.OK, JSONStoreConstants.JSON_STORE_RC_OK);
                    }
                    catch (JSONStoreException jsonException)
                    {
                        // catch a JSONStore specific exception and return the error code
                        return new StorageResult(Status.ERROR, jsonException.errorCode);
                    }
                    catch (Exception) {
                        return new StorageResult(Status.ERROR, JSONStoreConstants.JSON_STORE_PERSISTENT_STORE_FAILURE);
                    }
                 }, token));
        }
        
        public IAsyncOperation<StorageResult> clear(string parameters)
        {
            return AsyncInfo.Run((token) =>
                Task.Run<StorageResult>(() =>
                {
                    try
                    {
                        // deserialize the parameters passed in as an array of strings
                        string[] paramStrings = JsonConvert.DeserializeObject<string[]>(parameters);

                        // the first is the collection name
                        string collectionName = paramStrings[0];

                        // get the JSONStoreCollection to add the new data to
                        JSONStoreCollection collection = JSONStore.getCollectionWithName(collectionName);

                        if (collection == null)
                        {
                            return new StorageResult(Status.ERROR, JSONStoreConstants.JSON_STORE_DATABASE_NOT_OPEN);
                        }
                        else
                        {
                            collection.clearCollection();
                            return new StorageResult(Status.OK, JSONStoreConstants.JSON_STORE_RC_OK);
                        }
                    }
                    catch (JSONStoreException jsonException)
                    {
                        // catch a JSONStore specific exception and return the error code
                        return new StorageResult(Status.ERROR, jsonException.errorCode);
                    }
                    catch (Exception)
                    {
                        return new StorageResult(Status.ERROR, JSONStoreConstants.JSON_STORE_PERSISTENT_STORE_FAILURE);
                    }
                }, token));
        }

        public IAsyncOperation<StorageResult> change(string parameters)
        {
           return AsyncInfo.Run((token) =>
                Task.Run<StorageResult>(() =>
                {
                    try
                    {
                        // deserialize the parameters passed in as an array of strings
                        string[] paramStrings = JsonConvert.DeserializeObject<string[]>(parameters);

                        // the first is the collection name
                        string collectionName = paramStrings[0];

                        // the data to be changed in the collection
                        JArray dataArray = JArray.Parse(paramStrings[1]);

                        // the third argument is a JSON object of options
                        JSONStoreReplaceOptions options = JsonConvert.DeserializeObject<JSONStoreReplaceOptions>(paramStrings[2]);

                        // get the JSONStoreCollection to add the new data to
                        JSONStoreCollection collection = JSONStore.getCollectionWithName(collectionName);

                        if (collection == null)
                        {
                            return new StorageResult(Status.ERROR, JSONStoreConstants.JSON_STORE_DATABASE_NOT_OPEN);
                        }
                        else
                        {
                            int updatedOrAdded = collection.changeData(dataArray, options);
                            return new StorageResult(Status.OK, updatedOrAdded);
                        }
                    }
                    catch (JSONStoreException jsonException)
                    {
                        // catch a JSONStore specific exception and return the error code
                        return new StorageResult(Status.ERROR, jsonException.errorCode);
                    }
                    catch (Exception)
                    {
                        return new StorageResult(Status.ERROR, JSONStoreConstants.JSON_STORE_PERSISTENT_STORE_FAILURE);
                    }
                }, token));
        }

        public IAsyncOperation<StorageResult> startTransaction(string parameters)
        {
            return AsyncInfo.Run((token) =>
                Task.Run<StorageResult>(() =>
                {
                    try
                    {

                        // deserialize the parameters passed in as an array of strings
                        string[] paramStrings = JsonConvert.DeserializeObject<string[]>(parameters);

                        //start the transaction
                        bool worked = JSONStore.startTransaction();
                        int rc = worked ? JSONStoreConstants.JSON_STORE_RC_OK : JSONStoreConstants.JSON_STORE_PERSISTENT_STORE_FAILURE;
                        return new StorageResult(Status.OK, rc);
                    }
                    catch (JSONStoreException jsonException)
                    {
                        //JSONStoreLoggerException(exception);

                        // catch a JSONStore specific exception and return the error code
                        return new StorageResult(Status.ERROR, jsonException.errorCode);
                    }
                    catch (Exception)
                    {
                        return new StorageResult(Status.ERROR, JSONStoreConstants.JSON_STORE_PERSISTENT_STORE_FAILURE);
                    }
                }, token));
        }

        public IAsyncOperation<StorageResult> commitTransaction(string parameters)
        {
            return AsyncInfo.Run((token) =>
                Task.Run<StorageResult>(() =>
                {
                    try
                    {

                        // deserialize the parameters passed in as an array of strings
                        string[] paramStrings = JsonConvert.DeserializeObject<string[]>(parameters);

                        //start the transaction
                        bool worked = JSONStore.commitTransaction();
                        int rc = worked ? JSONStoreConstants.JSON_STORE_RC_OK : JSONStoreConstants.JSON_STORE_PERSISTENT_STORE_FAILURE;
                        return new StorageResult(Status.OK, rc);
                    }
                    catch (JSONStoreException jsonException)
                    {
                        //JSONStoreLoggerException(exception);

                        // catch a JSONStore specific exception and return the error code
                        return new StorageResult(Status.ERROR, jsonException.errorCode);
                    }
                    catch (Exception)
                    {
                        return new StorageResult(Status.ERROR, JSONStoreConstants.JSON_STORE_PERSISTENT_STORE_FAILURE);
                    }
                }, token));
        }

        public IAsyncOperation<StorageResult> rollbackTransaction(string parameters)
        {
            return AsyncInfo.Run((token) =>
                Task.Run<StorageResult>(() =>
                {
                    try
                    {

                        // deserialize the parameters passed in as an array of strings
                        string[] paramStrings = JsonConvert.DeserializeObject<string[]>(parameters);

                        //start the transaction
                        bool worked = JSONStore.rollbackTransaction();
                        int rc = worked ? JSONStoreConstants.JSON_STORE_RC_OK : JSONStoreConstants.JSON_STORE_PERSISTENT_STORE_FAILURE;
                        return new StorageResult(Status.OK, rc);
                    }
                    catch (JSONStoreException jsonException)
                    {
                        //JSONStoreLoggerException(exception);

                        // catch a JSONStore specific exception and return the error code
                        return new StorageResult(Status.ERROR, jsonException.errorCode);
                    }
                    catch (Exception)
                    {
                        return new StorageResult(Status.ERROR, JSONStoreConstants.JSON_STORE_PERSISTENT_STORE_FAILURE);
                    }
                }, token));
        }

        public IAsyncOperation<StorageResult> fileInfo(string parameters)
        {
            return AsyncInfo.Run((token) =>
                Task.Run<StorageResult>(() =>
                {
                    try
                    {

                        // deserialize the parameters passed in as an array of strings
                        string[] paramStrings = JsonConvert.DeserializeObject<string[]>(parameters);

                        //get the file info
                        var fileInfo = JSONStore.fileInfo();
                        fileInfo.Wait();
                        return new StorageResult(Status.OK, fileInfo.Result);
                    }
                    catch (JSONStoreException jsonException)
                    {
                        //JSONStoreLoggerException(exception);

                        // catch a JSONStore specific exception and return the error code
                        return new StorageResult(Status.ERROR, jsonException.errorCode);
                    }
                    catch (Exception)
                    {
                        return new StorageResult(Status.ERROR, JSONStoreConstants.JSON_STORE_PERSISTENT_STORE_FAILURE);
                    }
                }, token));
        }
    }
}
