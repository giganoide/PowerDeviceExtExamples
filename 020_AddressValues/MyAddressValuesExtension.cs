using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Atys.PowerDvc;
using Atys.PowerDvc.Drivers;
using Atys.PowerDvc.Services;
using Atys.PowerSuite;
using Atys.PowerSuite.Extensibility;
using Atys.PowerSuite.Scheduler;

namespace TeamSystem.Customizations
{
    [ExtensionData("MyAddressValuesExtension", "Put here an extension description.", "1.0",
        Author = "Author Name", EditorCompany = "Company Name")]
    public class MyAddressValuesExtension : IExtension<IDvcSystemManager>
    {

        #region const

        private const string LOGGERSOURCE = @"MYADDRESSVALUESEXTENSION";

        #endregion

        #region fields

        private IDvcSystemManager _ServiceManager = null; //Riferimento a PowerDEVICE
        private readonly Guid _ComponentId = Guid.NewGuid(); //id specifico del componente (per scheduler)
        private Guid _WriteWatchDogActivityId = Guid.Empty; //id task per job schedulatore

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
            this._ServiceManager.InitializationCompleted += this.ServiceManager_InitializationCompleted;

            this.SetupWriteWatchDogActivity();
        }

        /// <inheritdoc />
        public void Shutdown()
        {
            this.ClearWriteWatchDogActivity();
        }

        #endregion

        private void ServiceManager_InitializationCompleted(object sender, EventArgs e)
        {
            const string addressFqn = @"{OPCV1}{TESTOPC}{numeric.random.int32}";

            var tokenResolver = this._ServiceManager.DriversContextRepository.TokenResolver;

            var addressToken = (ResolvedToken)tokenResolver.Resolve(addressFqn);
            if (addressToken == null || !addressToken.IsSolved)
            {
                this._ServiceManager.AppendMessageToLog(MessageLevel.Warning, LOGGERSOURCE,
                                                        "ServiceManager_InitializationCompleted(): token not solved");
                return;
            }

            /*
             * in questo caso posso creare event handlers
             * per i singoli address (oppure associare gli handlers ad eventi
             * di address diversi)
             *
             */

            addressToken.Address.ValueChanging += this.Address_ValueChanging;
            addressToken.Address.ValueChanged += this.Address_ValueChanged;
        }

        #region address value change

        private void Address_ValueChanging(object sender, ValueChangingCancelEventArgs e)
        {
            /*
             * evento generato dopo la lettura di un nuovo valore
             * e prima di applicarlo
             * CANCELLABILE
             */

            //e.CurrentValue: valore corrente
            //e.PreviousValue: valore precedente
            //e.NewValue: nuovo valore che sta per essere applicato
            //e.Cancel = true: annulla il cambio di valore
        }

        private void Address_ValueChanged(object sender, ValueChangedEventArgs e)
        {
            /*
             *evenato generato quando un nuovo valore è stato effettivamente applicato
             *
             */

            //e.CurrentValue: nuovo valore, che quindi diventa il corrente
            //e.PreviousValue: valore precedente
        }

        #endregion

        #region watchdog example

        /*
         * un costruttore di macchine ci ha chiesto di rendere più solida
         * la comunicazione con un meccanismo di watch dog, cioè i dati di PowerDevice
         * sono considerati validi solo se lo stesso vari ain continuazione
         * un indirizzo specifico. Ne consegue che utilizziamo lo schedulatore
         * interno del servizio per variare in continuazione un valore
         * (intero incrementato di uno ogni secondo)
         */

        private void SetupWriteWatchDogActivity()
        {
            this._ServiceManager.AppendMessageToLog(MessageLevel.Diagnostics, LOGGERSOURCE,
                                                                "SetupWriteWatchDogActivity(): called");

            //prendo un riferimento al servizio di schedulazione
            var scheduler = this._ServiceManager.Services.GetServiceOfType<ISchedulerService>();

            var interval = TimeSpan.FromSeconds(1);
            var firstStart = DateTimeOffset.Now.AddMinutes(1);

            //creo l'oggetto per lo schedulatore
            var recurringActivity = new RecurringActivity(Guid.NewGuid(),
                                                          this._ComponentId,
                                                          firstStart,
                                                          this.WriteWatchDogTaskTriggerAction,
                                                          this.WriteWatchDogTaskErrorAction,
                                                          interval);
            //creo il job nello schedulatore
            if (scheduler.SubmitRecurringActivity(recurringActivity))
            {
                this._WriteWatchDogActivityId = recurringActivity.ActivityId;
                this._ServiceManager.AppendMessageToLog(MessageLevel.Diagnostics, LOGGERSOURCE,
                                             "SetupWriteWatchDogActivity(): activity submitted -> "
                                             + "'{0}' starting from: {1} interval: {2}",
                                             recurringActivity.ActivityId.ToString(),
                                             firstStart.ToString(), interval.ToString());
            }
            else
            {
                this._ServiceManager.AppendMessageToLog(MessageLevel.Diagnostics, LOGGERSOURCE,
                                             "SetupWriteWatchDogActivity(): failed to submit activity.");
            }
        }

        private void ClearWriteWatchDogActivity()
        {
            this._ServiceManager.AppendMessageToLog(MessageLevel.Diagnostics, LOGGERSOURCE,
                                         "ClearWriteWatchDogActivity(): called");

            if (this._WriteWatchDogActivityId == Guid.Empty)
                return;

            //prendo un riferimento al servizio di schedulazione
            var scheduler = this._ServiceManager.Services.GetServiceOfType<ISchedulerService>();
            Debug.Assert(scheduler != null);

            if (scheduler.HasActivityById(this._WriteWatchDogActivityId))
                scheduler.CancelActivity(this._WriteWatchDogActivityId);

            //tolgo il riferimento a task
            this._WriteWatchDogActivityId = Guid.Empty;
        }

        private void WriteWatchDogTaskTriggerAction(Guid activityId)
        {
            Debug.Assert(activityId != Guid.Empty);

            this._ServiceManager.AppendMessageToLog(MessageLevel.Info, LOGGERSOURCE,
                                         "WriteWatchDogTaskTriggerAction(): called");

            try
            {
                this.WriteWatchDogForMachine();
            }
            catch (Exception ex)
            {
                this._ServiceManager.AppendExceptionToLog(LOGGERSOURCE, ex, "Error working on machine watch dog.");
            }

        }

        private void WriteWatchDogTaskErrorAction(Guid activityId, Exception ex)
        {
            Debug.Assert(activityId != Guid.Empty);
            Debug.Assert(ex != null);

            this._ServiceManager.AppendExceptionToLog(LOGGERSOURCE, ex, "Error working on machine watch dog.");
        }

        private void WriteWatchDogForMachine()
        {
            const int maxValue = 32000;
            const string watchDogFqn = @"{OPCV1}{TESTOPC}{storage.numeric.reg01}";

            var tokenResolver = this._ServiceManager.DriversContextRepository.TokenResolver;

            var watchDogToken = (ResolvedToken)tokenResolver.Resolve(watchDogFqn);
            if (watchDogToken == null || !watchDogToken.IsSolved)
            {
                this._ServiceManager.AppendMessageToLog(MessageLevel.Warning, LOGGERSOURCE,
                                                        "WriteWatchDogForMachine(): token not solved");
                return;
            }

            if (watchDogToken.Address.Device.Connector.State != DriverConnectionState.Connected)
            {
                this._ServiceManager.AppendMessageToLog(MessageLevel.Diagnostics, LOGGERSOURCE,
                                                        "WriteWatchDogForMachine(): device not connected");
                return;
            }

            var valueToWrite = 0;

            if (watchDogToken.Address.HasConsistentValue)
            {
                var watchDogValue = watchDogToken.Address.CurrentValue.GetConvertedValueToType<int>();
                if (watchDogValue < maxValue)
                    valueToWrite = watchDogValue + 1;
            }

            /*
             * NB: necessario PER ORA referenziare anche Atys.PowerDvc.Internals.dll
             *     per poter utilizzare ValueContainerFactory
             *     Questo meccanismo verrà variato nelle prossime versioni
             *
             */

            var writeValueContainer = ValueContainerFactory.GetSpecificValueContainer(watchDogToken.Address.ValueType, valueToWrite);

            var writeRes = watchDogToken.Address.SetOutputValue(writeValueContainer);

            this._ServiceManager.AppendMessageToLog(MessageLevel.Info, LOGGERSOURCE,
                                                    "WriteWatchDogForMachine(): write watchDogValue = {0} (val = {1})",
                                                    writeRes, valueToWrite);
        }

        #endregion
    }
}
