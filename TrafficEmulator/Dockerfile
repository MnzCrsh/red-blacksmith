FROM grafana/k6:2.4.0

WORKDIR /opt/traffic-emulator

COPY proto ./proto
COPY lib ./lib
COPY organic.js ./organic.js
COPY black-friday.js ./black-friday.js
COPY ddos-simulation.js ./ddos-simulation.js

ENTRYPOINT ["k6"]
CMD ["run", "organic.js"]
