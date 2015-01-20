using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using SpendingDataAccess;
using System.IO;
using MyTraceLogger;
using System.Diagnostics;

namespace SpendingLogic
{
    public class SpendingLogic
    {
        public bool successCategorized;
        public List<aTransaction> unCategorizedTransactionList{get;private set;}
        public List<CategorizedTransaction> categorizedTransactionList { get; private set; }

        public Dictionary<string, int> filterCategoryList { get; private set; }

        public Dictionary<string, int> categoryList { get; private set; }

        private static MyTraceSourceLogger myLogger;

        public SpendingLogic()
        {
            unCategorizedTransactionList = new List<aTransaction>();
            categorizedTransactionList = new List<CategorizedTransaction>();
            filterCategoryList = GetFilterCategoriesList();
            categoryList = GetCategoriesList();
            successCategorized = false;

            myLogger = new MyTraceSourceLogger("SpendingLogicTrace");

            if(myLogger==null)
            {
                throw new NullReferenceException("Unable to initialize logger.");
            }
        }
        public List<displayCategorizedTransaction> GetDisplayCategorizedTransaction(DateTime beginTransaction, DateTime endTransaction)
        {
            List<displayCategorizedTransaction> wantedTransactionList = null;
            SpendingDataAccess.SpendingDataAccess DataAccess = new SpendingDataAccess.SpendingDataAccess();

            wantedTransactionList = DataAccess.GetDisplayCategorizedTransaction(beginTransaction, endTransaction);

            return wantedTransactionList;
        }

        public Dictionary<string, int> GetCategoriesList()
        {
            Dictionary<string, int> CategoriesList = null;

            SpendingDataAccess.SpendingDataAccess DataAccess = new SpendingDataAccess.SpendingDataAccess();
            CategoriesList = DataAccess.GetCategoriesList();
            return CategoriesList;
        }

        public Dictionary<string, int> GetFilterCategoriesList()
        {
            Dictionary<string, int> FilterCategoriesList = null;

            SpendingDataAccess.SpendingDataAccess DataAccess = new SpendingDataAccess.SpendingDataAccess();
            FilterCategoriesList = DataAccess.GetFilterCategoriesList();
            return FilterCategoriesList;
        }

        private bool SaveDataToDB(List<CategorizedTransaction> categorizedTransactionList, Dictionary<string, int> filterCategoryList)
        {
            bool bReturn = false;

            SpendingDataAccess.SpendingDataAccess DataAccess = new SpendingDataAccess.SpendingDataAccess();

            bReturn = DataAccess.SaveDataToDB(categorizedTransactionList, filterCategoryList);

            return bReturn;
        }

        public bool AnalyzeMyNewFiles(List<FileInfo> listFileInfo)
        {            
            bool bReturn = false;
                //read each file and load data into a list then pass it to the next page to categorize the data
            //delete these files once done and update the listFileInfo
                foreach (FileInfo file in listFileInfo.ToList())
                {
                    List<aTransaction> tmpTransactions = new List<aTransaction>();
                    tmpTransactions = ReadCSV(file);

                    if (tmpTransactions.Count > 0)
                    {
                        myLogger.TraceEvent(TraceEventType.Information, 3, string.Format("{0} transaction[s] loaded from file: {1}.", tmpTransactions.Count, file.Name));
                        unCategorizedTransactionList.AddRange(tmpTransactions);
                        
                        //now have raw transactions, will try to categorized it with filters in the db

                        if (CategorizeTransactionListWithExistingFilters(tmpTransactions, categorizedTransactionList))
                        {
                            successCategorized = true;
                            bReturn = true;
                        }
                        else
                        {
                            myLogger.TraceEvent(TraceEventType.Warning, 2, "Unable to categorize all transactions with existing filters data.");
                        }
                    }
                    else
                    {
                        myLogger.TraceEvent(TraceEventType.Error, 1, string.Format("No transaction found in file: {0}.", file.Name));
                    }

                    //remove the file once we done reading
                    try
                    {
                        listFileInfo.Remove(file);
                        file.Delete();                        
                    }
                    catch(Exception ex)
                    {
                        myLogger.TraceEvent(TraceEventType.Error, 1, string.Format("Unable to delete file: {0}. {1}", file.Name,ex.Message));
                    }
                }
                return bReturn;                
        }

        //Read .csv file and load transaction into List<aTransaction>
        private List<aTransaction> ReadCSV(FileInfo file)
        {
            List<aTransaction> listTransactions = new List<aTransaction>();
            DateTime myDate;
            string myDescription = "";
            string myCheck = "";
            decimal myAmount = 0.00M;
            int seekPosition = 0;
            string tmpString = "";
            int line = 0;

            try
            {
                    //will attempt to read all lines in the file
                    //each line with error will be log and ignore and continue to the next line
                using (StreamReader myStreamReader = new StreamReader(file.FullName))
                {
                    while (myStreamReader.Peek() >= 0)
                    {
                        //Date,Description,Check Number,Amount
                        string aReadLine = myStreamReader.ReadLine();
                        //string[] tmpLineArray = aReadLine.Split(',');
                        //since the description could be in the format of "description,lou,ky"
                        //a string split is not the way to go
                        //get the first index of comma then pull the date out
                        //then get the last index of comma and pull the amount out

                        if (aReadLine.Length > 0)
                        {
                            line++;

                            seekPosition = aReadLine.IndexOf(',');
                            tmpString = aReadLine.Remove(seekPosition);

                            if (DateTime.TryParse(tmpString, out myDate))
                            {
                                //remove the date
                                aReadLine = aReadLine.Substring(seekPosition + 1);
                                seekPosition = aReadLine.LastIndexOf(',');

                                tmpString = aReadLine.Substring(seekPosition + 1);

                                if (decimal.TryParse(tmpString, out myAmount))
                                {
                                    //remove the amount
                                    aReadLine = aReadLine.Remove(seekPosition);
                                    //get the description
                                    seekPosition = aReadLine.LastIndexOf('"');
                                    tmpString = aReadLine.Remove(seekPosition + 1);

                                    myDescription = tmpString;

                                    //get the last remaining of check
                                    aReadLine = aReadLine.Substring(seekPosition + 1);

                                    myCheck = aReadLine.Substring(1, aReadLine.Length - 1);

                                    aTransaction myTransaction = new aTransaction(myDate, myDescription, myCheck, myAmount);
                                    listTransactions.Add(myTransaction);
                                }
                                else
                                {
                                    myLogger.TraceEvent(TraceEventType.Error, 1, string.Format("Unable to convert amount. {0} in file {1}.", aReadLine, file.Name));
                                }
                            }
                            else
                            {
                                //1st column ned to be a Date
                                myLogger.TraceEvent(TraceEventType.Warning, 2, string.Format("Unable to convert date. {0} in file {1}.", aReadLine, file.Name));
                            }
                        }
                        else
                        {
                            //invalid Data, need to be 4 column
                            myLogger.TraceEvent(TraceEventType.Warning, 2, "This line is empty. {0}", file.Name);
                        }
                    }
                }
            }
            catch (UnauthorizedAccessException ex)
            {
                myLogger.TraceEvent(TraceEventType.Error, 1, string.Format("Unable to access file: {0}.", ex.Message));
                throw;
            }
            catch (FileNotFoundException ex) 
            {
                myLogger.TraceEvent(TraceEventType.Error, 1, string.Format("File not found: {0}.", ex.Message));
                throw;
            }
            catch(Exception ex)
            {
                myLogger.TraceEvent(TraceEventType.Error, 1, string.Format("Unknow exception: {0}.", ex.Message));
                throw;
            }

            return listTransactions;
        }


        //Try to categorize List<aTransaction> into List<CategorizedTransaction>
        //After this method run, transaction[s] that can't be categorize will left in List<aTransaction> transactionlist
        //Transaction[s] that were able to categorized will be store in List<CategorizedTransaction>
        private bool CategorizeTransactionListWithExistingFilters(List<aTransaction> transactionlist, List<CategorizedTransaction>  categorizedTransactionList)
        {            
            bool bReturn = false;

            if(filterCategoryList.Count > 0)
            { 
                //can't delete an item from a list in a foreach loop of that list
                //going to loop backward.
                foreach (aTransaction transaction in transactionlist.ToArray())
                {
                    if (CategorizeSingleTransactionWithExistingFilterPhrase(transaction, filterCategoryList, categorizedTransactionList))
                    {
                        //remove the transaction once its categorized
                        transactionlist.Remove(transaction);
                    }
                }
            }
            else
            {
                myLogger.TraceEvent(TraceEventType.Warning, 2, "No filter phrase found.");
            }

            if (transactionlist.Count() == 0)
            {
                bReturn = true;
            }

            return bReturn;
        }

        //No duplication of transaction will be allow to add
        public bool CategorizeSingleTransactionWithExistingFilterPhrase(aTransaction transaction, Dictionary<string, int> oldFilterCategoryList, List<CategorizedTransaction> newCagetorizedTransactionList)
        {
            bool bReturn = false;

            foreach (KeyValuePair<string, int> filter in oldFilterCategoryList)
            {
                if (transaction.Description.Contains(filter.Key.ToString()))
                {
                    CategorizedTransaction aTransaction = new CategorizedTransaction(transaction.myDate, transaction.Description, transaction.check, transaction.Amt, filter.Value, filter.Key);
                    if (!newCagetorizedTransactionList.Contains(aTransaction))
                    {
                        newCagetorizedTransactionList.Add(aTransaction);
                    }
                    else
                    {
                        //duplicated transaction will not be add to the list
                        myLogger.TraceEvent(TraceEventType.Warning, 2, string.Format("Duplicated transaction found: {0}.", aTransaction.ToString()));
                    }

                    bReturn = true;
                }
            }

            return bReturn;
        }
    }
}
