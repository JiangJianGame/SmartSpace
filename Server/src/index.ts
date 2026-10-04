import config, { listen } from "@colyseus/tools";
import { PlazaRoom } from "./rooms/PlazaRoom";

const appConfig = config({
    initializeGameServer: (gameServer) => {
        gameServer.define("plaza", PlazaRoom);
    },
    initializeExpress: (app) => {
        app.get("/", (req, res) => {
            res.send("SmartSpace Colyseus Server is Running!");
        });
        app.get("/health", (req, res) => {
            res.json({ status: "ok", time: new Date().toISOString() });
        });
    }
});

const port = Number(process.env.PORT || 2567);
listen(appConfig, port);
