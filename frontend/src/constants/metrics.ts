export const metrics = [
  {
    code: 'DELIVERY_DELAY',
    name: 'Delivery delay',
    type: 'number',
    unit: 'minutes'
  },
  {
    code: 'TEMPERATURE',
    name: 'Temperature breach',
    type: 'number',
    unit: '°C'
  },
  {
    code: 'MISSING_DOCS',
    name: 'Missing documents',
    type: 'boolean',
    unit: 'count'
  }
] as const;

