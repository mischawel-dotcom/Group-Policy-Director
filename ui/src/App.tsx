import { Navigate, Route, Routes } from 'react-router-dom'
import './App.css'
import { AppShell } from './components/AppShell'
import { GpdProvider } from './gpd/GpdContext'
import { AdminView } from './views/AdminView'
import { AuditView } from './views/AuditView'
import { ChangesView } from './views/ChangesView'
import { ContainersView } from './views/ContainersView'
import { PoliciesView } from './views/PoliciesView'

export default function App() {
  return (
    <GpdProvider>
      <Routes>
        <Route element={<AppShell />}>
          <Route index element={<Navigate to="/policies" replace />} />
          <Route path="policies" element={<PoliciesView />} />
          <Route path="changes" element={<ChangesView />} />
          <Route path="containers" element={<ContainersView />} />
          <Route path="admin" element={<AdminView />} />
          <Route path="audit" element={<AuditView />} />
          <Route path="*" element={<Navigate to="/policies" replace />} />
        </Route>
      </Routes>
    </GpdProvider>
  )
}
