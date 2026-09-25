
1. React (what we have build till now ); Modern 
2. make sure everything is running ;
3. caching (redis)
4. event driven (where we can do ) (using service emulater) ;


UI 

1. list page (product)
2. without login 
3. if the user is purchasing something then he need to login 
4. login page 
5. register page 
6. product details page 
7. admin seed when the app starts 
8. Basket should product;
9. user can write the review 
10. mock the payment done and download the invoice
11. mail service mail is received when the 
the order is confirmed
















### passwords 

New Admin 
Click Add to basket, then log in with admin@eshop.com / Admin@123

# User 
new user : sagarshashank92@gmail.com
password : Password@123

Running the project


cd eshop-distributed\src
dotnet run --project AppHost


https://<webapp-host>:<port>/login
Default seeded credentials to test with:

Email: admin@eshop.com
Password: Admin@123

# Running cmd
cd eshop-distributed\src
dotnet run --project AppHost




Here is the plan in order. Each step builds on the one before it.

## Step 0: Finish React (now)
- Run the app and test the React pages. Tell me if anything is broken, and I'll fix it.
- **Commit** the React + Bff work.
- Once you're happy with React, **delete the old Blazor WebApp**.

## Step 1: Mock payment (your item 10)
**Why first:** checkout does not work yet without Stripe keys. Every later step (events, email) happens *after* payment, so payment must work first.
- Add a setting `Payment:Mode = Mock` (default) or `Stripe`.
- In Mock mode, "Proceed to checkout" goes to a simple "Pay now" page. Click it, the order becomes **Paid**, and you land on the confirmation page.
- The invoice download already works once the order is Paid.
- **Size:** small

## Step 2: Event-driven + email (your items 4 and 11)
- When an order is paid, Orders sends an **`OrderPaid` event** to RabbitMQ.
- **Basket** listens and clears the basket. This replaces today's direct call, which uses a hidden admin token.
- A new **Notification service** listens and sends the "Order confirmed" email.
- Add **Mailpit**, a fake email server, in Aspire. It has a web inbox where you can see every email the app sends. No real email goes out.
- Also fix the old bug: the price-change event updates only one fixed user's (`"swn"`) basket.
- **Size:** medium

## Step 3: Redis caching (your item 3)
- Cache the product list and product details in Redis, inside the Catalog service.
- When an admin adds, edits or deletes a product, clear the cache so users always see correct data.
- **Size:** small

## Step 4: Product reviews (your item 9)
- Backend: a new reviews table in the Catalog database, plus APIs to list reviews and add one (logged-in users only).
- React: show the reviews and the average ⭐ rating on the product details page, with a form to write a review.
- **Size:** medium

## Step 5: Clean up
- Remove the Counter/Weather leftovers, if they're still there after Blazor is deleted.
- Update the old packages that show security warnings (OpenTelemetry 1.11).
- Update the README: how to run, login details, what each service does.
- **Size:** small

## Already done from your list ✅
- Product list without login
- Login required before buying
- Login and Register pages
- Product details page
- Admin created automatically at startup
- Basket

## Later (after all of this)
- Hosting on AWS: containers on ECS, database on RDS, Redis on ElastiCache, RabbitMQ on Amazon MQ.

**Right now:** test React (Step 0) and tell me the result. Once it works, I'll start **Step 1 (mock payment)**.

I can also update your `ReamaningTask.md` with this plan, so you can tick things off as we go.

