export type RuleTemplate = {
  key: string;
  name: string;
  metric: string;
  conditionType: 'threshold' | 'range' | 'boolean';
  operator?: string;
  threshold?: number;
  minValue?: number;
  maxValue?: number;
  eventType?: string;
};

export const ruleTemplates: RuleTemplate[] = [
  {
    key: 'delivery_delay',
    name: 'Late delivery',
    metric: 'DELIVERY_DELAY',
    conditionType: 'threshold',
    operator: '>',
    threshold: 30
  },
  {
    key: 'temperature',
    name: 'Temperature breach',
    metric: 'TEMPERATURE',
    conditionType: 'range',
    minValue: -25,
    maxValue: -18
  },
  {
    key: 'missing_docs',
    name: 'Missing documents',
    metric: 'MISSING_DOCS',
    conditionType: 'boolean',
    eventType: 'DOCUMENT_MISSING'
  }
];

