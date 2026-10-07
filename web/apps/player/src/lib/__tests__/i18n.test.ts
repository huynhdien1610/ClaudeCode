import test from "node:test";
import assert from "node:assert/strict";
import { en, vi, zhHans, zhHant } from "../dict.ts";
import { detectLocale, translate, errorMessage, formatNumber } from "../i18n.ts";

const keys = Object.keys(en).sort();

test("BR-I18N-02: mọi ngôn ngữ có đủ khóa dịch, không thừa khóa", () => {
  for (const [name, d] of Object.entries({ vi, zhHans, zhHant })) {
    assert.deepEqual(Object.keys(d).sort(), keys, `${name} lệch khóa so với en`);
  }
});

test("Không có chuỗi rỗng và biến {x} nhất quán giữa các ngôn ngữ", () => {
  const vars = (s: string) => (s.match(/\{\w+\}/g) ?? []).sort().join(",");
  for (const k of keys as (keyof typeof en)[]) {
    for (const d of [vi, zhHans, zhHant]) {
      assert.ok(d[k].trim().length > 0, `${k} rỗng`);
      assert.equal(vars(d[k]), vars(en[k]), `${k}: biến không khớp`);
    }
  }
});

test("BR-I18N-01: nhận diện ngôn ngữ trình duyệt", () => {
  assert.equal(detectLocale("vi-VN"), "vi");
  assert.equal(detectLocale("zh-TW"), "zh-Hant");
  assert.equal(detectLocale("zh-HK"), "zh-Hant");
  assert.equal(detectLocale("zh-CN"), "zh-Hans");
  assert.equal(detectLocale("fr-FR"), "en");
  assert.equal(detectLocale(undefined), "en");
});

test("translate thay biến; mã lỗi lạ dùng thông điệp chung", () => {
  assert.equal(translate("en", "store.payCoin", { n: 1000 }), "Buy with 1000 Coin");
  assert.equal(errorMessage("vi", "INSUFFICIENT_BALANCE"), "Số dư không đủ.");
  assert.equal(errorMessage("en", "SOMETHING_NEW"), en["err.GENERIC"]);
});

test("Định dạng số theo locale", () => {
  assert.equal(formatNumber("en", 1234567), "1,234,567");
  assert.equal(formatNumber("vi", 1234567), "1.234.567");
});

test("Mọi mã lỗi backend thường gặp đều có thông điệp riêng", () => {
  const codes = ["INVALID_CREDENTIALS", "INSUFFICIENT_BALANCE", "PACK_ALREADY_OPENED", "CARD_NOT_FORGEABLE", "FORGE_DAILY_LIMIT", "OTP_LOCKED", "COIN_TO_GEM_DAILY_LIMIT", "PHONE_VERIFICATION_REQUIRED"];
  for (const c of codes) assert.notEqual(errorMessage("zh-Hans", c), translate("zh-Hans", "err.GENERIC"), c);
  for (const c of codes) assert.ok(`err.${c}` in en, `thiếu err.${c}`);
});
