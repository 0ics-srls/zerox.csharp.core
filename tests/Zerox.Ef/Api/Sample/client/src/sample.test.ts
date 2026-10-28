// The example application through its generated client and the typed load: run by
// ClientTests (Zerox.Ef.Api.Tests) against the Sample on a real port, seeded with 3 customers (Acme/Rome,
// Bolt/Milan, Cargo) and 25 orders (order i: customer i % 3, Amount i * 10, Closed when i % 5 == 0).
// The type checks below are proved by tsc (npm run check), the behaviour by node:test (npm test).
import assert from 'node:assert/strict';
import { test } from 'node:test';
import { customersCreate, customersDelete, customersGetById } from './api/customers/customers.ts';
import type { Customer, Order, OrderStatus } from './api/model/index.ts';
import { ordersLoad } from './api/orders/orders.ts';
import { load, LoadError, type GroupOf } from './load.ts';

type Equal<A, B> = (<X>() => X extends A ? 1 : 2) extends (<X>() => X extends B ? 1 : 2) ? true : false;
const typed = <T extends true>(): T | undefined => undefined;

test('rows: whole orders, sorted and paged on the server', async () => {
  const result = await load(ordersLoad, { Sort: [{ Selector: 'Amount', Desc: true }], Take: 3, RequireTotalCount: true });

  typed<Equal<typeof result.data, Order[]>>();
  assert.deepEqual(result.data.map(o => o.Amount), [250, 240, 230]);
  assert.equal(result.totalCount, 25);
});

test('rows: a filter through a navigation', async () => {
  const result = await load(ordersLoad, { Filter: ['Customer.Name', '=', 'Cargo'], Take: 100, RequireTotalCount: true });

  assert.equal(result.totalCount, 8);
  assert.ok(result.data.every(o => o.CustomerId !== undefined));
});

test('columns: only what Select asks, names through the navigation', async () => {
  const result = await load(ordersLoad, { Select: ['Amount', 'Customer.Name'], Sort: [{ Selector: 'Amount' }], Take: 1 });

  typed<Equal<typeof result.data, { Amount?: number; Customer?: { Name?: string } | null }[]>>();
  assert.deepEqual(result.data, [{ Amount: 10, Customer: { Name: 'Bolt' } }]);
});

test('groups: keys, counts and sums, never rows', async () => {
  const result = await load(ordersLoad, {
    Group: [{ Selector: 'Status' }],
    GroupSummary: [{ Selector: 'Amount', SummaryType: 'sum' }],
  });

  typed<Equal<typeof result.data, GroupOf<null>[]>>();
  const byKey = new Map(result.data.map(g => [g.key as OrderStatus, g]));
  assert.equal(byKey.get('Open')?.count, 20);
  assert.equal(byKey.get('Closed')?.summary?.[0], 750);
  assert.ok(result.data.every(g => g.items === null));
});

test('groups on two levels: the inner groups, never rows', async () => {
  const result = await load(ordersLoad, { Group: [{ Selector: 'Status' }, { Selector: 'Customer.Name' }] });

  typed<Equal<typeof result.data, GroupOf<GroupOf<null>[]>[]>>();
  const inner = result.data.flatMap(g => g.items);
  assert.equal(inner.reduce((sum, g) => sum + (g.count ?? 0), 0), 25);
  assert.ok(inner.every(g => g.items === null));
});

test('count: no rows, the total', async () => {
  const result = await load(ordersLoad, { IsCountQuery: true });

  assert.equal(result.totalCount, 25);
  assert.deepEqual(result.data, []);
});

test('an invalid request: the ApiError of the server', async () => {
  await assert.rejects(load(ordersLoad, { Take: 5000 }), (e: unknown) =>
    e instanceof LoadError && e.status === 400 && e.error.Code === 'InvalidRequest' && e.error.Message.includes('Take'));
});

test('fields the entity does not have do not compile', () => {
  const never = () => {
    // @ts-expect-error not a field of Order
    void load(ordersLoad, { Select: ['Nope'], Take: 1 });
    // @ts-expect-error not a field of the Customer of an Order
    void load(ordersLoad, { Filter: ['Customer.Street', '=', 'x'], Take: 1 });
    // @ts-expect-error a reserved field is not in the contract
    void load(ordersLoad, { Sort: [{ Selector: 'InternalNote' }], Take: 1 });
  };
  assert.equal(typeof never, 'function');
});

test('the CRUD through the generated client: 201, 200, 204, 404', async () => {
  const created = await customersCreate({ Name: 'Delta', City: 'Turin' });
  assert.equal(created.status, 201);
  const customer: Customer = created.status === 201 ? created.data : assert.fail('not created');

  const read = await customersGetById(customer.Id ?? 0);
  assert.equal(read.status, 200);
  assert.equal(read.status === 200 ? read.data.City : undefined, 'Turin');

  const deleted = await customersDelete(customer.Id ?? 0, { version: customer.Version ?? 0 });
  assert.equal(deleted.status, 204);
  assert.equal((await customersGetById(customer.Id ?? 0)).status, 404);
});
