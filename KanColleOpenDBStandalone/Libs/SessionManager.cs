using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Nekoxy;

using Grabacr07.KanColleWrapper.Models;

namespace KanColleOpenDBStandalone.Libs
{
	internal class SessionManager
	{
		private List<Action<Session>> list;

		public SessionManager()
		{
			list = new List<Action<Session>>();
		}

		public ManagedSession Prepare()
		{
			return new ManagedSession(this);
		}
		public void Register(Action<Session> sessionEvent)
		{
			this.list.Add(sessionEvent);
		}
		public void Call(Session session)
		{
			foreach (var sess in list)
				sess?.Invoke(session);
		}
	}

	internal class ManagedSession
	{
		private event Func<Session, bool> _handlerWhere;
		protected Delegate[] handlerWhere => this._handlerWhere?.GetInvocationList();

		protected Action<Session> parseEvent { get; set; }
		protected SvData svData;

		protected SessionManager manager;

		public ManagedSession(SessionManager manager)
		{
			this.manager = manager;
		}
		public ManagedSession(SessionManager manager, ManagedSession session) : this(manager)
		{
			this._handlerWhere = session._handlerWhere;
		}

		public ManagedSession Where(Func<Session, bool> predicate)
		{
			this._handlerWhere += predicate;
			return this;
		}
		public ManagedSession TryParse()
		{
			parseEvent = (session) => SvData.TryParse(session, out this.svData);
			return this;
		}
		public ManagedSession<T> TryParse<T>()
		{
			var x = new ManagedSession<T>(manager, this);
			return x.TryParse();
		}
		public ManagedSession Subscribe(Action<SvData> onEvent)
		{
			this.manager.Register((session) =>
			{
				foreach (var i in handlerWhere)
					if (!(bool)i.DynamicInvoke(session)) return;

				this.parseEvent?.Invoke(session);
				if (this.svData != null) onEvent?.Invoke(this.svData);
			});
			return this;
		}
		public ManagedSession SubscribeRaw(Action<Session> onEvent)
		{
			this.manager.Register((session) =>
			{
				foreach (var i in handlerWhere)
					if (!(bool)i.DynamicInvoke(session)) return;

				onEvent?.Invoke(session);
			});
			return this;
		}
	}
	internal class ManagedSession<T> : ManagedSession
	{
		protected new SvData<T> svData;

		public ManagedSession(SessionManager manager) : base(manager)
		{
		}
		public ManagedSession(SessionManager manager, ManagedSession session) : base(manager, session)
		{
		}

		public new ManagedSession<T> TryParse()
		{
			parseEvent = (session) => SvData.TryParse(session, out this.svData);
			return this;
		}
		public ManagedSession<T> Subscribe(Action<SvData<T>> onEvent)
		{
			this.manager.Register((session) =>
			{
				foreach (var i in handlerWhere)
					if (!(bool)i.DynamicInvoke(session)) return;

				this.parseEvent?.Invoke(session);
				if (this.svData != null) onEvent?.Invoke(this.svData);
			});
			return this;
		}
	}
}
