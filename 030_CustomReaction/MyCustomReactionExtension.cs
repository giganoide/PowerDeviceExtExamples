using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Atys.PowerDvc;
using Atys.PowerDvc.Drivers;
using Atys.PowerSuite;
using Atys.PowerSuite.Extensibility;

namespace TeamSystem.Customizations
{
    [ExtensionData("MyCustomReactionExtension", "Put here an extension description.", "1.0",
        Author = "Author Name", EditorCompany = "Company Name")]
    public class MyCustomReactionExtension : IExtension<IDvcSystemManager>
    {

        #region const

        private const string LOGGERSOURCE = @"MYCUSTOMREACTIONEXTENSION";

        #endregion

        #region fields

        private IDvcSystemManager _ServiceManager = null; //Riferimento a PowerDEVICE

        #endregion


        #region IExtension<IDvcSystemManager> Members

        /// <inheritdoc />
        public void Initialize(IDvcSystemManager serviceManager)
        {
#if DEBUG
            System.Diagnostics.Debugger.Launch();
#endif

            //memorizzo il riferimento all'oggetto principale di PowerDEVICE
            this._ServiceManager = serviceManager;

            //strumenti per messaggi di DIAGNOSTICA:

            //questa istruzione inserisce un messaggio nel file di log di PowerDEVICE
            this._ServiceManager.AppendMessageToLog(MessageLevel.Diagnostics, LOGGERSOURCE, "Estensione creata!");
            //mentre la successiva invia un messaggio ai Clients
            this._ServiceManager.SendMessageToUserInterface(MessageLevel.Diagnostics, LOGGERSOURCE, "Estensione creata!");
        }

        /// <inheritdoc />
        public void Run()
        {
            this._ServiceManager.SynchronizedCustomReactionRaised += ServiceManager_SynchronizedCustomReactionRaised;
        }

        /// <inheritdoc />
        public void Shutdown()
        {
            this._ServiceManager.SynchronizedCustomReactionRaised -= ServiceManager_SynchronizedCustomReactionRaised;
        }

        #endregion

        /// <summary>
        /// Gestione evento custom sincronizzato
        /// per salvataggio dati fascio
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        void ServiceManager_SynchronizedCustomReactionRaised(object sender, SynchronizedCustomReactionEventArgs e)
        {
            const string customReactionDisplayName = @"CUSTOM_SYNC";
            const string strobeFqn = @"{S7_TRAFITAL}{COMBINATA-3}{DB51.DBD66}"; //TODO

            if (e.Reaction.Action.DisplayName != customReactionDisplayName
               || e.Reaction.Token.FullyQualifiedName != strobeFqn)
            {
                this._ServiceManager.AppendMessageToLog(MessageLevel.Diagnostics, LOGGERSOURCE,
                                                        "ServiceManager_SynchronizedCustomReactionRaised(): not handled...");
                return;
            }

            this._ServiceManager.AppendMessageToLog(MessageLevel.Info, LOGGERSOURCE,
                                                    "ServiceManager_SynchronizedCustomReactionRaised(): handling...");

            var strobeCounterValue = e.Reaction.Token.Address.CurrentValue.GetConvertedValueToType<int>();
            var timestamp = e.Reaction.ReactionTime.ToString("yyyyMMddHHmmss");

            var insertResult = this.InsertDataToDatabase(strobeCounterValue, timestamp);

            e.Success = insertResult;
        }

        private bool InsertDataToDatabase(int addressValue, string timestamp)
        {
            //EX_EDU01_DVCCUSTOM

            const string commandText = @"INSERT INTO [dbo].[EX_EDU01_DVCCUSTOM]([AddressValue],[ReactionDate]) VALUES (@AddressValue,@ReactionDate)";

            var result = false;

            var connectionString = this.BuildDatabaseConnectionString();

            var command = new SqlCommand(commandText);
            command.Parameters.Add(new SqlParameter() { ParameterName = "@AddressValue", SqlDbType = SqlDbType.NVarChar, Value = addressValue.ToString(), Direction = ParameterDirection.Input });
            command.Parameters.Add(new SqlParameter() { ParameterName = "@ReactionDate", SqlDbType = SqlDbType.NVarChar, Value = timestamp ?? string.Empty, Direction = ParameterDirection.Input });

            var opRes = this.ExecuteSqlNonQueryCommand(connectionString, command, true);

            result = (opRes > 0);

            return result;
        }

        private string BuildDatabaseConnectionString()
        {
            //Pattern per la costruzione della stringa di connessione a PowerMES: AtysPowerMES - PowerMES - atys$mes
            const string connectionStringPattern = @"Data Source={0};Initial Catalog={1};"
                                                   + @"Persist Security Info=True;"
                                                   + @"User ID={2};Password={3}";

            string dbInstanceName = this._ServiceManager.ApplicationSettings.DataTracing.DatabaseInstanceName;
            string dataBaseName = this._ServiceManager.ApplicationSettings.DataTracing.DatabaseName;
            string userName = @"PowerMES";
            string password = @"atys$mes";

            return string.Format(connectionStringPattern,
                                 dbInstanceName, dataBaseName,
                                 userName, password);
        }

        /// <summary>
        /// Esegue il comando SQL specificato e restituisce il numero
        /// di record interessati dal comando
        /// </summary>
        /// <param name="connectionString">Stringa di connessione</param>
        /// <param name="command"></param>
        /// <param name="throwOnError">Se in caso di errore deve essere propagata l'eccezione.</param>
        /// <returns>Numero di record interessati dal comando</returns>
        private int ExecuteSqlNonQueryCommand(string connectionString, SqlCommand command, bool throwOnError)
        {
            Debug.Assert(!string.IsNullOrWhiteSpace(connectionString));
            Debug.Assert(command != null);

            if (command == null)
                throw new ArgumentNullException(nameof(command));
            if (string.IsNullOrWhiteSpace(connectionString))
                throw new ArgumentNullException(nameof(connectionString));

            SqlConnection connection = new SqlConnection(connectionString);

            int result = 0;
            try
            {
                command.Connection = connection;
                connection.Open();

                result = command.ExecuteNonQuery();
            }
            catch (Exception)
            {
                if (throwOnError)
                    throw;
            }
            finally
            {
                if (connection.State == System.Data.ConnectionState.Open)
                    connection.Close();
                connection.Dispose();
            }
            return result;
        }

    }
}
