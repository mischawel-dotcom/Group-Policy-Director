import type { PolicyTreeNode } from '../types'

export function TreeNodeView({
  node,
  depth = 0,
  canEdit,
  onChange,
}: {
  node: PolicyTreeNode
  depth?: number
  canEdit: boolean
  onChange: (path: string, patch: { state?: string; value?: string }) => void
}) {
  const editable = Boolean(canEdit && node.isEditable && !node.isStub)

  return (
    <li className={node.isStub ? 'tree-stub' : undefined}>
      <div className="tree-row" style={{ paddingLeft: depth * 14 }}>
        <span className="tree-label">
          {node.name}
          {node.isStub ? <em className="stub-tag"> Stub</em> : null}
        </span>
        {editable && node.settingType === 'PolicyState' ? (
          <select
            value={node.state ?? 'NotConfigured'}
            onChange={(e) => onChange(node.path, { state: e.target.value })}
            aria-label={`State for ${node.name}`}
          >
            <option value="NotConfigured">Not Configured</option>
            <option value="Enabled">Enabled</option>
            <option value="Disabled">Disabled</option>
          </select>
        ) : node.state ? (
          <em className="tree-state">{node.state}</em>
        ) : null}
        {editable && node.settingType === 'Text' ? (
          <input
            className="tree-value-input"
            value={node.value ?? ''}
            placeholder="Wert"
            onChange={(e) => onChange(node.path, { value: e.target.value })}
            aria-label={`Value for ${node.name}`}
          />
        ) : node.value ? (
          <code className="tree-value">{node.value}</code>
        ) : null}
      </div>
      {node.children?.length ? (
        <ul>
          {node.children.map((c) => (
            <TreeNodeView
              key={c.path}
              node={c}
              depth={depth + 1}
              canEdit={canEdit}
              onChange={onChange}
            />
          ))}
        </ul>
      ) : null}
    </li>
  )
}

export function applyLocalPatch(
  root: PolicyTreeNode,
  path: string,
  patch: { state?: string; value?: string },
): PolicyTreeNode {
  if (root.path === path) {
    return { ...root, ...patch }
  }
  return {
    ...root,
    children: root.children.map((c) => applyLocalPatch(c, path, patch)),
  }
}
