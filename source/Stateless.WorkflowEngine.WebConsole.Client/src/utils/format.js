const numberFormat = new Intl.NumberFormat()
const dateFormat = new Intl.DateTimeFormat(undefined, { dateStyle: 'medium', timeStyle: 'medium' })

export function formatNumber(value) {
  return value === null || value === undefined ? '-' : numberFormat.format(value)
}

/**
 * Formats a UTC date from the server in the browser's local time.  Unset .NET dates (0001-01-01) are shown
 * as blank.
 */
export function formatDate(value) {
  if (!value) return ''
  const date = new Date(value)
  if (Number.isNaN(date.getTime()) || date.getUTCFullYear() <= 1) return ''
  return dateFormat.format(date)
}

export function connectionName(connection) {
  if (!connection) return ''
  return `${connection.host}${connection.port ? ':' + connection.port : ''}/${connection.database}`
}
