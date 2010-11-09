﻿using System;
using System.Linq;
using System.Reflection;
using FluentNHibernate.Cfg;
using NHibernate;
using NHibernate.Cfg;
using NHibernate.Linq;

namespace LiteFx.Bases.Context.NHibernate
{
    /// <summary>
    /// NHibernate base context.
    /// </summary>
    /// <typeparam name="TId"></typeparam>
    public abstract class NHibernateContextBase<TId> : IContext<TId>, IDisposable
        where TId : IEquatable<TId>
    {
        #region NHibernate Configuration and SessionFactory Cache
        private static Configuration _cfg;
        /// <summary>
        /// Propriedade privada para fazer o cache da configuração do NHibernate.
        /// </summary>
        protected static Configuration Cfg
        {
            get { return _cfg ?? (_cfg = new Configuration()); }
        }

        /// <summary>
        /// Has to be setted on constructor.
        /// </summary>
        protected static Assembly AssemblyToConfigure { get; set; }

        /// <summary>
        /// Private sessionFactory.
        /// </summary>
        private static ISessionFactory _sessionFactory;

        /// <summary>
        /// Propriedade privada para fazer o cache do sessionFactory do NHibernate.
        /// </summary>
        protected static ISessionFactory SessionFactory
        {
            get
            {
                return _sessionFactory ?? (_sessionFactory = Fluently.Configure(Cfg)
                                                                 .Mappings(m =>
                                                                               {
                                                                                   m.FluentMappings.AddFromAssembly(
                                                                                       AssemblyToConfigure);
                                                                                   m.HbmMappings.AddFromAssembly(
                                                                                       AssemblyToConfigure);
                                                                               })
                                                                 .BuildSessionFactory());
            }
        }
        #endregion

        /// <summary>
        /// The NHibernate Database Context Constructor.
        /// </summary>
        protected NHibernateContextBase(Assembly assemblyToConfigure)
        {
            AssemblyToConfigure = assemblyToConfigure;
            OpenSession();
        }

        /// <summary>
        /// Open the session with the database.
        /// </summary>
        protected virtual void OpenSession()
        {
            currentSession = SessionFactory.OpenSession();
        }

        #region IDBContext Members

        /// <summary>
        /// Sessão com o banco de dados.
        /// </summary>
        protected ISession currentSession;

        /// <summary>
        /// Flag usado para identificar se há uma transação aberta.
        /// </summary>
        protected bool openTransaction;

        /// <summary>
        /// Variavel que mantem a transação.
        /// </summary>
        protected ITransaction transaction;

        /// <summary>
        /// Inicia uma transação e retorna a referência da transação como um IDisposable.
        /// </summary>
        /// <returns>Referência da transação como um IDisposable.</returns>
        public virtual IDisposable BeginTransaction()
        {
            if (openTransaction)
                return transaction;

            if (currentSession != null)
                currentSession.Dispose();

            OpenSession();

            if (currentSession != null) 
                transaction = currentSession.BeginTransaction();

            openTransaction = true;
            return transaction;
        }

        /// <summary>
        /// Salva as alterações realizadas sobre a transação aberta no banco de dados.
        /// </summary>
        public virtual void CommitTransaction()
        {
            if (!openTransaction)
                throw new Exception("Este método pode ser chamado somente após a chamada do método BeginTransaction.");

            try
            {
                transaction.Commit();
            }
            finally
            {
                transaction.Dispose();
                transaction = null;
                openTransaction = false;
            }
        }

        /// <summary>
        /// Descarta as alterações realizadas sobre a transação aberta.
        /// </summary>
        public virtual void RollBackTransaction()
        {
            if (!openTransaction)
                throw new Exception("Este método pode ser chamado somente após a chamada do método BeginTransaction.");

            transaction.Rollback();
            transaction.Dispose();
            openTransaction = false;
        }

        /// <summary>
        /// Get a queryable object of an especifique entity.
        /// </summary>
        /// <typeparam name="T">Entity type.</typeparam>
        /// <returns>Queryable object.</returns>
        public virtual IQueryable<T> GetQueryableObject<T>() where T : EntityBase<TId>
        {
            return currentSession.Linq<T>();
        }

        /// <summary>
        /// Exclui uma entidade do contexto pelo seu Identificador.
        /// </summary>
        /// <typeparam name="T">Tipo do entidade.</typeparam>
        /// <param name="id">Identificador do entidade.</param>
        public virtual T Delete<T>(TId id)
        {
            var obj = currentSession.Get<T>(id);
            Delete(obj);
            return obj;
        }

        /// <summary>
        /// Exclui uma entidade do contexto.
        /// </summary>
        /// <param name="entity">Entidade que será exlcuida.</param>
        public virtual void Delete(object entity)
        {
            BeginTransaction();
            currentSession.Delete(entity);
        }

        /// <summary>
        /// Salva uma entidade no contexto.
        /// </summary>
        /// <param name="entity">Entidade que será salva.</param>
        public virtual void Save(object entity)
        {
            BeginTransaction();
            currentSession.SaveOrUpdate(entity);
        }

        /// <summary>
        /// Remove o objeto do cache do contexto.
        /// </summary>
        /// <param name="entity">Objeto a ser removido do cache.</param>
        public virtual void RemoveFromCache(object entity)
        {
            currentSession.Evict(entity);
        }

        /// <summary>
        /// Salva as informações alteradas no contexto no banco de dados.
        /// </summary>
        public virtual void SaveContext()
        {
            CommitTransaction();
        }

        #endregion

        #region IDisposable Members [Dispose pattern implementation]

        /// <summary>
        /// Implementação do Dipose Pattern.
        /// </summary>
        /// <remarks><a target="blank" href="http://msdn.microsoft.com/en-us/library/fs2xkftw.aspx">Dispose Pattern</a>.</remarks>
        private bool disposed;

        /// <summary>
        /// Libera todos os recursos utilizados pela classe.
        /// Implementação do Dispose Pattern.
        /// </summary>
        /// <remarks><a target="blank" href="http://msdn.microsoft.com/en-us/library/fs2xkftw.aspx">Dispose Pattern</a>.</remarks>
        /// <param name="disposing">Usado para verificar se a chamada esta sendo feita pelo <see cref="GC"/> ou pela aplicação.</param>
        protected virtual void Dispose(bool disposing)
        {
            if (disposed) return;

            if (disposing)
            {
                if (openTransaction)
                {
                    RollBackTransaction();
                }

                if (currentSession != null)
                    currentSession.Dispose();
            }

            disposed = true;
        }

        /// <summary>
        /// Chamado pelo <see ref="GC" /> para liberar recursos que não estão sendo utilizados.
        /// Implementação do Dipose Pattern.
        /// </summary>
        /// <remarks><a target="blank" href="http://msdn.microsoft.com/en-us/library/fs2xkftw.aspx">Dispose Pattern</a>.</remarks>
        ~NHibernateContextBase()
        {
            Dispose(false);
        }

        /// <summary>
        /// Libera todos os recursos utilizados pela classe.
        /// Implementação do Dipose Pattern.
        /// </summary>
        /// <remarks><a target="blank" href="http://msdn.microsoft.com/en-us/library/fs2xkftw.aspx">Dispose Pattern</a>.</remarks>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
        #endregion
    }
}
