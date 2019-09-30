using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Atys.PowerDvc;
using Atys.PowerSuite;
using Atys.PowerSuite.Extensibility;

namespace TeamSystem.Customizations
{
    [ExtensionData("MyEmptyExtension", "Put here an extension description.", "1.0",
        Author = "Author Name", EditorCompany = "Company Name")]
    public class MyEmptyExtension : IExtension<IDvcSystemManager>
    {

        #region const

        private const string LOGGERSOURCE = @"MYEMPTYEXTENSION";

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

            /*
             * Your custom implementation here...
             * --
             * Fornire di seguito l'implementazione del metodo...
             */
        }

        /// <inheritdoc />
        public void Run()
        {
            /*
             * Your custom implementation here...
             * (Attach to application events, if needed)
             * --
             * Fornire di seguito l'implementazione del metodo...
             * (Se necessario creare qui i gestori eventi applicazione)
             */
        }

        /// <inheritdoc />
        public void Shutdown()
        {
            /*
             * Your custom implementation here...
             * (Detach from application events)
             * --
             * Fornire di seguito l'implementazione del metodo...
             * (Rilasciare eventuale gestione eventi applicazione)
             */
        }

        #endregion

    }
}
