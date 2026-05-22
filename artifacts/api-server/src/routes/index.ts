import { Router, type IRouter } from "express";
import healthRouter from "./health.js";
import stockRouter from "./stock.js";

const router: IRouter = Router();

router.use(healthRouter);
router.use(stockRouter);

export default router;
