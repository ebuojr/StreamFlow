import http from 'k6/http';
import { check, sleep } from 'k6';

export const options = {
    vus: 5,
    duration: '30s',
    thresholds: {
        http_req_duration: ['p(95)<500'],
        http_req_failed: ['rate<0.01'],
    },
};

const BASE_URL = __ENV.BASE_URL || 'https://localhost:7033';

export default function () {
    // Fixed test data based on Danish order from CreateOrderForCountry
    const orderId = crypto.randomUUID();
    const customerId = crypto.randomUUID();
    
    const order = {
        id: orderId,
        orderNo: 0,
        createdAt: new Date().toISOString(),
        orderState: "Pending",
        countryCode: "DK",
        totalAmount: 599.00,
        customerId: customerId,
        orderItems: [
            {
                id: crypto.randomUUID(),
                orderId: orderId,
                sku: "FASHION-001",
                name: "Plus Size Maxi Dress - Floral",
                quantity: 1,
                unitPrice: 599.00,
                totalPrice: 599.00,
                status: "Pending"
            }
        ],
        customer: {
            id: customerId,
            firstName: "Mette",
            lastName: "Hansen",
            email: "mette.hansen@example.dk",
            phone: "+45-55-66-77-88"
        },
        payment: {
            paymentMethod: "Credit Card",
            paymentStatus: "Authorized",
            paidAt: new Date().toISOString(),
            currency: "DKK",
            amount: 599.00
        },
        shippingAddress: {
            street: "Vestergade 123",
            city: "København",
            state: "Midtjylland",
            postalCode: "2100",
            country: "DK"
        }
    };

    const params = {
        headers: {
            'Content-Type': 'application/json',
        },
        insecureSkipTLSVerify: true,
    };

    const response = http.post(
        `${BASE_URL}/api/order`,
        JSON.stringify(order),
        params
    );

    check(response, {
        'status is 200': (r) => r.status === 200,
        'has orderNo': (r) => {
            try {
                const body = JSON.parse(r.body);
                return body.orderNo > 0;
            } catch (e) {
                return false;
            }
        },
        'isSuccessfullyCreated': (r) => {
            try {
                const body = JSON.parse(r.body);
                return body.isSuccessfullyCreated === true;
            } catch (e) {
                return false;
            }
        },
    });

    sleep(1);
}
