// The example application through its generated client and the typed load, against the Sample on a
// real port. expectTypeOf is checked by tsc, the rest at run time.
import type { Customer, Order } from "./api/model/index.ts";
import { type GroupOf, LoadError, type LoadRequest, load } from "./load.ts";
import {
  customersCreate,
  customersDelete,
  customersGetById,
  customersLoad,
} from "./api/customers/customers.ts";
import { describe, expect, expectTypeOf, test } from "vitest";
import { ordersLoad } from "./api/orders/orders.ts";

// The seed of the Sample: 3 customers (Acme/Rome, Bolt/Milan, Cargo) and 25 orders — order i belongs to
// customer i % 3, its Amount is i * 10, it is Closed when i % 5 == 0.
const ABOVE_THE_PAGE_LIMIT = 5000;
const ALL = 100;
const CARGO_ORDERS = 8;
const CLOSED_AMOUNT = 750;
const FIRST_AMOUNT = 250;
const NONE = 0;
const ONE = 1;
const OPEN_ORDERS = 20;
const ORDERS = 25;
const SECOND_AMOUNT = 240;
const SMALLEST_AMOUNT = 10;
const STATUS = {
  created: 201,
  deleted: 204,
  invalid: 400,
  missing: 404,
  ok: 200,
} as const;
const THIRD_AMOUNT = 230;
const TOP_AMOUNTS = [FIRST_AMOUNT, SECOND_AMOUNT, THIRD_AMOUNT];

describe("the load of orders", () => {
  test("rows: whole orders, sorted and paged on the server", async () => {
    const result = await load(ordersLoad, {
      RequireTotalCount: true,
      Sort: [{ Desc: true, Selector: "Amount" }],
      Take: TOP_AMOUNTS.length,
    });

    expectTypeOf(result.data).toEqualTypeOf<Order[]>();
    expect(result.data.map((order) => order.Amount)).toEqual(TOP_AMOUNTS);
    expect(result.totalCount).toBe(ORDERS);
  });

  test("rows: a filter through a navigation", async () => {
    const result = await load(ordersLoad, {
      Filter: ["Customer.Name", "=", "Cargo"],
      RequireTotalCount: true,
      Take: ALL,
    });

    expect(result.totalCount).toBe(CARGO_ORDERS);
  });

  test("columns: only what Select asks, names through the navigation", async () => {
    const result = await load(ordersLoad, {
      Select: ["Amount", "Customer.Name"],
      Sort: [{ Selector: "Amount" }],
      Take: ONE,
    });

    expectTypeOf(result.data).toEqualTypeOf<
      { Amount?: number; Customer?: { Name?: string } | null }[]
    >();
    expect(result.data).toEqual([
      { Amount: SMALLEST_AMOUNT, Customer: { Name: "Bolt" } },
    ]);
  });

  test("groups: keys, counts and sums, never rows", async () => {
    const result = await load(ordersLoad, {
        Group: [{ Selector: "Status" }],
        GroupSummary: [{ Selector: "Amount", SummaryType: "sum" }],
      }),
      byKey = new Map(result.data.map((group) => [String(group.key), group]));

    expectTypeOf(result.data).toEqualTypeOf<GroupOf<null>[]>();
    expect(byKey.get("Open")?.count).toBe(OPEN_ORDERS);
    expect(byKey.get("Closed")?.summary).toEqual([CLOSED_AMOUNT]);
    expect(result.data.map((group) => group.items)).toEqual([null, null]);
  });

  test("groups on two levels: the inner groups, never rows", async () => {
    const result = await load(ordersLoad, {
        Group: [{ Selector: "Status" }, { Selector: "Customer.Name" }],
      }),
      inner = result.data.flatMap((group) => group.items);

    expectTypeOf(result.data).toEqualTypeOf<GroupOf<GroupOf<null>[]>[]>();
    expect(inner.map((group) => group.count)).toSatisfy(
      (counts: (number | undefined)[]) =>
        counts.reduce<number>((sum, count) => sum + (count ?? NONE), NONE) ===
        ORDERS,
    );
  });

  test("count: no rows, the total", async () => {
    const result = await load(ordersLoad, { IsCountQuery: true });

    expect(result.totalCount).toBe(ORDERS);
    expect(result.data).toEqual([]);
  });

  test("an invalid request: the ProblemDetails of the server", async () => {
    const refused = load(ordersLoad, { Take: ABOVE_THE_PAGE_LIMIT });

    await expect(refused).rejects.toBeInstanceOf(LoadError);
    await expect(refused).rejects.toMatchObject({
      problem: { Code: "InvalidRequest", status: STATUS.invalid },
      status: STATUS.invalid,
    });
  });

  test("fields the entity does not have do not compile", () => {
    const filter: LoadRequest<Order> = {
      // @ts-expect-error not a field of the Customer of an Order
      Filter: ["Customer.Street", "=", "x"],
      Take: ONE,
    };
    const select: LoadRequest<Order> = {
      // @ts-expect-error not a field of Order
      Select: ["Nope"],
      Take: ONE,
    };
    const sort: LoadRequest<Order> = {
      // @ts-expect-error a reserved field is not in the contract
      Sort: [{ Selector: "InternalNote" }],
      Take: ONE,
    };

    expect([filter, select, sort]).toHaveLength(
      Object.keys({ filter, select, sort }).length,
    );
  });
});

describe("the load of customers", () => {
  test("rows: the customers in a city, their names only", async () => {
    const result = await load(customersLoad, {
      Filter: ["City", "=", "Rome"],
      Select: ["Name"],
      Take: ALL,
    });

    expectTypeOf(result.data).toEqualTypeOf<Pick<Customer, "Name">[]>();
    expect(result.data).toEqual([{ Name: "Acme" }]);
  });
});

describe("the CRUD through the generated client", () => {
  test("201, 200, 204, 404", async () => {
    const created = await customersCreate({ City: "Turin", Name: "Delta" });
    if (created.status !== STATUS.created) {
      throw new Error(`not created: ${String(created.status)}`);
    }
    const { Id: id, Version: version } = created.data;
    if (id === undefined || version === undefined) {
      throw new Error("the created customer has no Id or Version");
    }

    expect(await customersGetById(id)).toMatchObject({
      data: { City: "Turin" },
      status: STATUS.ok,
    });
    expect((await customersDelete(id, { version })).status).toBe(
      STATUS.deleted,
    );
    expect(await customersGetById(id)).toMatchObject({
      data: { Code: "NotFound" },
      status: STATUS.missing,
    });
  });
});
