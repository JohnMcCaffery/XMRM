using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using OpenSim.Region.OptionalModules.Scripting.Minimodule;

namespace OpenSim.Region.MRM.API.Scripting.Minimodule.ServerSide {
    class SchedulerWrapper : IScheduler {
        private IScheduler m_scheduler;

        public SchedulerWrapper (IScheduler scheduler) {
            m_scheduler = scheduler;
        }

        #region IScheduler Members

        public void RunIn(TimeSpan time) {
            m_scheduler.RunIn(time);
        }

        public void RunAndRepeat(TimeSpan time) {
            m_scheduler.RunAndRepeat(time);
        }

        public bool IfOccupied {
            get { return m_scheduler.IfOccupied; }
            set { m_scheduler.IfOccupied = value; }
        }

        public bool IfHealthy {
            get { return m_scheduler.IfHealthy; }
            set { m_scheduler.IfHealthy = value; }
        }

        public bool IfVisible {
            get { return m_scheduler.IfVisible; }
            set { m_scheduler.IfVisible = value; }
        }

        public bool Schedule {
            get { return m_scheduler.Schedule; }
            set { m_scheduler.Schedule = value; }
        }

        #endregion
    }
}
